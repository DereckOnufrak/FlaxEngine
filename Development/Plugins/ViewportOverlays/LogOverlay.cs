using System;
using System.Collections.Generic;
using FlaxEditor;
using FlaxEditor.SceneGraph;
using FlaxEngine;
using FlaxEngine.GUI;
using Object = FlaxEngine.Object;

namespace ViewportOverlays
{
    /// <summary>
    /// Collects log messages from the moment the plugin starts, so every log overlay can show the history. Thread-safe (logs can come from any thread).
    /// </summary>
    public static class LogBuffer
    {
        /// <summary>
        /// A single log message.
        /// </summary>
        public struct Entry
        {
            /// <summary>
            /// Sequential entry index.
            /// </summary>
            public long Index;

            /// <summary>
            /// The message type.
            /// </summary>
            public LogType Type;

            /// <summary>
            /// The message text.
            /// </summary>
            public string Message;

            /// <summary>
            /// The stack trace (can be empty).
            /// </summary>
            public string StackTrace;

            /// <summary>
            /// The ID of the context object (eg. actor or script), or empty.
            /// </summary>
            public Guid Context;

            /// <summary>
            /// The local time of the message.
            /// </summary>
            public DateTime Time;
        }

        private static readonly object _locker = new object();
        private static readonly List<Entry> _entries = new List<Entry>();
        private static long _nextIndex;
        private static int _clearVersion;
        private static bool _isRunning;

        /// <summary>
        /// The maximum amount of kept entries.
        /// </summary>
        public static int MaxEntries = 1000;

        /// <summary>
        /// Gets the index of the next added entry.
        /// </summary>
        public static long NextIndex
        {
            get
            {
                lock (_locker)
                    return _nextIndex;
            }
        }

        /// <summary>
        /// Gets the counter incremented on every <see cref="Clear"/>.
        /// </summary>
        public static int ClearVersion
        {
            get
            {
                lock (_locker)
                    return _clearVersion;
            }
        }

        /// <summary>
        /// Starts collecting the logs.
        /// </summary>
        public static void Start()
        {
            if (_isRunning)
                return;
            _isRunning = true;
            Debug.Logger.LogHandler.SendLog += OnSendLog;
            Debug.Logger.LogHandler.SendExceptionLog += OnSendExceptionLog;
        }

        /// <summary>
        /// Stops collecting the logs.
        /// </summary>
        public static void Stop()
        {
            if (!_isRunning)
                return;
            _isRunning = false;
            Debug.Logger.LogHandler.SendLog -= OnSendLog;
            Debug.Logger.LogHandler.SendExceptionLog -= OnSendExceptionLog;
        }

        /// <summary>
        /// Removes all entries.
        /// </summary>
        public static void Clear()
        {
            lock (_locker)
            {
                _entries.Clear();
                _clearVersion++;
            }
        }

        /// <summary>
        /// Copies the entries with index equal or greater than the given one.
        /// </summary>
        /// <param name="index">The first entry index to copy.</param>
        /// <param name="output">The output list.</param>
        /// <returns>The index of the next entry (use it for the next call).</returns>
        public static long CopySince(long index, List<Entry> output)
        {
            lock (_locker)
            {
                foreach (var e in _entries)
                {
                    if (e.Index >= index)
                        output.Add(e);
                }
                return _nextIndex;
            }
        }

        private static void Add(LogType type, string message, string stackTrace, Object context)
        {
            var entry = new Entry
            {
                Type = type,
                Message = message ?? string.Empty,
                StackTrace = stackTrace ?? string.Empty,
                Context = context ? context.ID : Guid.Empty,
                Time = DateTime.Now,
            };
            lock (_locker)
            {
                entry.Index = _nextIndex++;
                _entries.Add(entry);
                if (_entries.Count > MaxEntries)
                    _entries.RemoveRange(0, _entries.Count - MaxEntries);
            }
        }

        private static void OnSendLog(LogType level, string msg, Object o, string stackTrace)
        {
            Add(level, msg, stackTrace, o);
        }

        private static void OnSendExceptionLog(Exception exception, Object o)
        {
            Add(LogType.Error, exception.Message, exception.StackTrace, o);
        }
    }

    /// <summary>
    /// Overlay with the recent log messages. Click selects the context actor (if any), double-click copies the message.
    /// </summary>
    public sealed class LogOverlay : OverlayPanel
    {
        /// <summary>
        /// The maximum amount of displayed rows.
        /// </summary>
        public static int MaxRows = 500;

        private readonly Panel _scrollPanel;
        private readonly VerticalPanel _list;
        private readonly Button _infoButton;
        private readonly Button _warningButton;
        private readonly Button _errorButton;
        private readonly List<Button> _toolbarButtons = new List<Button>();
        private readonly List<LogBuffer.Entry> _newEntries = new List<LogBuffer.Entry>();
        private long _nextIndex;
        private int _clearVersion;
        private int _infoCount, _warningCount, _errorCount;
        private bool _showInfo = true, _showWarnings = true, _showErrors = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogOverlay"/> class.
        /// </summary>
        public LogOverlay()
        : base("Log")
        {
            var toolbar = new ContainerControl
            {
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, 24),
                Parent = Body,
            };
            float x = 4;
            _infoButton = AddToolbarButton(toolbar, ref x, "Info", () => _showInfo = !_showInfo);
            _warningButton = AddToolbarButton(toolbar, ref x, "Warnings", () => _showWarnings = !_showWarnings);
            _errorButton = AddToolbarButton(toolbar, ref x, "Errors", () => _showErrors = !_showErrors);
            AddToolbarButton(toolbar, ref x, "Clear", LogBuffer.Clear);

            _scrollPanel = new Panel(ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 26, 0),
                Parent = Body,
            };
            _list = new VerticalPanel
            {
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                IsScrollable = true,
                Spacing = 0,
                Pivot = Float2.Zero,
                Parent = _scrollPanel,
            };

            _clearVersion = LogBuffer.ClearVersion;
            UpdateButtons();
        }

        private Button AddToolbarButton(ContainerControl toolbar, ref float x, string text, Action toggle)
        {
            var width = Style.Current.FontMedium.MeasureText(text).X + 16;
            var button = new Button(x, 3, width, 18)
            {
                Text = text,
                Parent = toolbar,
            };
            button.Clicked += () =>
            {
                toggle();
                UpdateButtons();
                ApplyFilter();
            };
            _toolbarButtons.Add(button);
            x += width + 4;
            return button;
        }

        /// <inheritdoc />
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Sync with the shared buffer
            var clearVersion = LogBuffer.ClearVersion;
            if (clearVersion != _clearVersion)
            {
                _clearVersion = clearVersion;
                _list.DisposeChildren();
                _infoCount = _warningCount = _errorCount = 0;
                UpdateButtons();
            }
            if (LogBuffer.NextIndex != _nextIndex)
                AddNewEntries();
        }

        private void AddNewEntries()
        {
            _newEntries.Clear();
            _nextIndex = LogBuffer.CopySince(_nextIndex, _newEntries);
            if (_newEntries.Count == 0)
                return;

            var scrollBar = _scrollPanel.VScrollBar;
            var wasAtBottom = scrollBar == null || !scrollBar.Visible || scrollBar.TargetValue >= scrollBar.Maximum - 4.0f;

            _list.IsLayoutLocked = true;
            OverlayListItem last = null;
            foreach (var e in _newEntries)
                last = AddRow(e);
            while (_list.ChildrenCount > MaxRows)
                _list.Children[0].Dispose();
            _list.IsLayoutLocked = false;
            _list.PerformLayout();
            UpdateButtons();

            if (wasAtBottom && last != null)
                _scrollPanel.ScrollViewTo(last, true);
        }

        private OverlayListItem AddRow(LogBuffer.Entry e)
        {
            var icons = Editor.Instance.Icons;
            var row = new OverlayListItem($"[{e.Time:HH:mm:ss}] {FirstLine(e.Message)}")
            {
                Tag = e,
                TooltipText = string.IsNullOrEmpty(e.StackTrace) ? e.Message : e.Message + "\n\n" + e.StackTrace,
                Parent = _list,
            };
            switch (e.Type)
            {
            case LogType.Warning:
                row.Icon = icons.Warning64;
                row.TextColor = Color.Yellow;
                _warningCount++;
                break;
            case LogType.Error:
            case LogType.Fatal:
                row.Icon = icons.Error64;
                row.TextColor = Color.OrangeRed;
                _errorCount++;
                break;
            default:
                row.Icon = icons.Info64;
                _infoCount++;
                break;
            }
            row.Visible = IsTypeVisible(e.Type);
            row.Clicked += OnRowClicked;
            row.DoubleClicked += OnRowDoubleClicked;
            return row;
        }

        private void OnRowClicked(OverlayListItem row)
        {
            if (row.Tag is LogBuffer.Entry e && e.Context != Guid.Empty)
            {
                // Select the actor that logged the message (scripts select their actor)
                var obj = Object.Find<Object>(ref e.Context);
                var actor = obj as Actor ?? (obj as Script)?.Actor;
                var node = actor ? SceneGraphFactory.FindNode(actor.ID) : null;
                if (node != null)
                    Editor.Instance.SceneEditing.Select(node);
            }
        }

        private void OnRowDoubleClicked(OverlayListItem row)
        {
            if (row.Tag is LogBuffer.Entry e)
                Clipboard.Text = string.IsNullOrEmpty(e.StackTrace) ? e.Message : e.Message + "\n" + e.StackTrace;
        }

        private bool IsTypeVisible(LogType type)
        {
            switch (type)
            {
            case LogType.Warning: return _showWarnings;
            case LogType.Error:
            case LogType.Fatal: return _showErrors;
            default: return _showInfo;
            }
        }

        private void ApplyFilter()
        {
            _list.IsLayoutLocked = true;
            foreach (var child in _list.Children)
            {
                if (child.Tag is LogBuffer.Entry e)
                    child.Visible = IsTypeVisible(e.Type);
            }
            _list.IsLayoutLocked = false;
            _list.PerformLayout();
        }

        private void UpdateButtons()
        {
            var style = Style.Current;
            SetButton(_infoButton, $"Info {_infoCount}", _showInfo, style);
            SetButton(_warningButton, $"Warnings {_warningCount}", _showWarnings, style);
            SetButton(_errorButton, $"Errors {_errorCount}", _showErrors, style);
            Title = _errorCount != 0 ? $"Log ({_errorCount} errors)" : "Log";

            // Widths change with the counters so lay the buttons out again
            float x = 4;
            foreach (var button in _toolbarButtons)
            {
                button.X = x;
                x += button.Width + 4;
            }
        }

        private static void SetButton(Button button, string text, bool isOn, Style style)
        {
            button.Text = text;
            button.Width = style.FontMedium.MeasureText(text).X + 16;
            button.BackgroundColor = isOn ? style.BackgroundSelected : style.BackgroundNormal;
        }

        private static string FirstLine(string text)
        {
            var newLine = text.IndexOf('\n');
            return newLine == -1 ? text : text.Substring(0, newLine) + " ...";
        }
    }
}
