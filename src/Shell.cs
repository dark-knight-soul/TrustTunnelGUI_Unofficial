using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TrustTunnelGui
{
    class MainWindow : Window
    {
        readonly Grid _pages = new Grid();
        readonly Grid _dialogLayer = new Grid { Visibility = Visibility.Collapsed };
        readonly Grid _snackLayer = new Grid { IsHitTestVisible = true, VerticalAlignment = VerticalAlignment.Bottom };
        readonly List<RadioButton> _navItems = new List<RadioButton>();
        readonly List<Page> _stack = new List<Page>();
        DialogAction _dialogCancel;
        DispatcherTimer _snackTimer;

        public bool AllowClose;

        public MainWindow()
        {
            Title = L.AppName;
            Width = 1040;
            Height = 720;
            MinWidth = 720;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = Theme.Font;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Icon = App.LoadImage("app_icon.ico");
            SourceInitialized += delegate { Theme.ApplyTitleBar(this); };
            PreviewKeyDown += OnPreviewKeyDown;
            BuildLayout();
            SelectTab(0);
        }

        // Recreates the whole UI with the current theme and language, then lets the caller restore navigation.
        public void Rebuild(Action restore)
        {
            foreach (var p in _stack) p.OnRemoved();
            _stack.Clear();
            CloseDialog();
            BuildLayout();
            Theme.ApplyTitleBar(this);
            if (restore != null) restore();
            else SelectTab(CurrentTab);
        }

        void BuildLayout()
        {
            Title = L.AppName + (App.IsElevated ? L.AdminSuffix : "");
            Background = C.BackgroundSystem;
            _navItems.Clear();
            _pages.Children.Clear();
            _dialogLayer.Children.Clear();
            _snackLayer.Children.Clear();
            // Detach the reused containers from the previous layout before building the new one.
            var oldCard = _pages.Parent as Border;
            if (oldCard != null) oldCard.Child = null;
            var oldRoot = Content as Grid;
            if (oldRoot != null) oldRoot.Children.Clear();

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rail = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };
            rail.Children.Add(NavItem(L.Servers, Icons.Dns, 0));
            rail.Children.Add(NavItem(L.Routing, Icons.Route, 1));
            rail.Children.Add(NavItem(L.Settings, Icons.Settings, 2));
            layout.Children.Add(rail);

            // ScaffoldWrapper: white rounded card on the system background.
            var card = new Border { Background = C.Background, CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 16, 16, 16), Child = _pages };
            UI.ClipRounded(card, 16);
            Grid.SetColumn(card, 1);
            layout.Children.Add(card);

            var root = new Grid();
            root.Children.Add(layout);
            root.Children.Add(_dialogLayer);
            root.Children.Add(_snackLayer);
            Content = root;
        }

        RadioButton NavItem(string label, Geometry icon, int index)
        {
            var rb = new RadioButton { Style = UI.S("NavItem"), Content = label, Tag = icon, GroupName = "nav" };
            rb.Checked += delegate { if (!_switchingTab) OpenTab(index); };
            _navItems.Add(rb);
            return rb;
        }

        bool _switchingTab;
        public int CurrentTab { get; private set; }

        public void SelectTab(int index)
        {
            _switchingTab = true;
            _navItems[index].IsChecked = true;
            _switchingTab = false;
            OpenTab(index);
        }

        void OpenTab(int index)
        {
            CurrentTab = index;
            Page page;
            switch (index)
            {
                case 1: page = new RoutingPage(this); break;
                case 2: page = new SettingsPage(this); break;
                default: page = new ServersPage(this); break;
            }
            Reset(page);
        }

        // ---- Navigation stack ----

        public void Reset(Page page)
        {
            foreach (var p in _stack) p.OnRemoved();
            _stack.Clear();
            _pages.Children.Clear();
            Push(page);
        }

        public void Push(Page page)
        {
            if (_stack.Count > 0) _stack[_stack.Count - 1].Visibility = Visibility.Collapsed;
            _stack.Add(page);
            _pages.Children.Add(page);
            page.OnShown();
        }

        // Pops the top page, asking to discard unsaved changes first unless force is set.
        public void Pop(bool force = false)
        {
            if (_stack.Count <= 1) return;
            var top = _stack[_stack.Count - 1];
            if (!force && top.IsDirty)
            {
                ShowDialog(L.DiscardChangesTitle, UI.BodyMedium(L.DiscardChangesDescription),
                    DialogAction.Cancel(),
                    new DialogAction(L.DiscardChanges, delegate { Pop(true); return true; }) { Danger = true });
                return;
            }
            _stack.RemoveAt(_stack.Count - 1);
            _pages.Children.Remove(top);
            top.OnRemoved();
            var prev = _stack[_stack.Count - 1];
            prev.Visibility = Visibility.Visible;
            prev.OnShown();
        }

        public Page TopPage { get { return _stack.Count > 0 ? _stack[_stack.Count - 1] : null; } }

        void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            if (_dialogLayer.Visibility == Visibility.Visible)
            {
                if (_dialogCancel != null && _dialogCancel.Handler()) CloseDialog();
                else if (_dialogCancel == null) CloseDialog();
                e.Handled = true;
            }
            else if (_stack.Count > 1)
            {
                Pop();
                e.Handled = true;
            }
        }

        // ---- Dialogs (AlertDialog) ----

        public bool IsDialogOpen { get { return _dialogLayer.Visibility == Visibility.Visible; } }

        public void ShowDialog(string title, UIElement content, params DialogAction[] actions)
        {
            _dialogLayer.Children.Clear();
            var scrim = new Border { Background = C.Scrim };
            scrim.MouseLeftButtonDown += delegate
            {
                if (_dialogCancel != null && _dialogCancel.Handler()) CloseDialog();
            };
            _dialogLayer.Children.Add(scrim);

            var body = new StackPanel();
            var titleBlock = UI.Text(title, 24);
            titleBlock.Margin = new Thickness(0, 0, 0, 16);
            body.Children.Add(titleBlock);
            if (content != null) body.Children.Add(content);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
            _dialogCancel = null;
            foreach (var a in actions)
            {
                var action = a;
                var b = UI.TextButton(action.Text, delegate { if (action.Handler()) CloseDialog(); }, action.Danger);
                b.Margin = new Thickness(8, 0, 0, 0);
                b.IsDefault = action.IsDefault;
                buttons.Children.Add(b);
                if (action.IsCancel) _dialogCancel = action;
            }
            body.Children.Add(buttons);

            var card = new Border
            {
                Background = C.Elevated,
                CornerRadius = new CornerRadius(28),
                Padding = new Thickness(24),
                MinWidth = 312,
                MaxWidth = 560,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(48),
                Child = body,
                Effect = UI.Shadow(6, 24, 0.25)
            };
            _dialogLayer.Children.Add(card);
            _dialogLayer.Visibility = Visibility.Visible;
            _dialogLayer.Opacity = 0;
            _dialogLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));

            // Focus the first input inside the dialog, if any.
            Dispatcher.BeginInvoke(new Action(delegate
            {
                var input = FindFirst<TextBox>(card);
                if (input != null) { input.Focus(); input.SelectAll(); }
                else card.Focus();
            }), DispatcherPriority.Input);
        }

        public void CloseDialog()
        {
            _dialogLayer.Visibility = Visibility.Collapsed;
            _dialogLayer.Children.Clear();
            _dialogCancel = null;
        }

        static T FindFirst<T>(DependencyObject root) where T : DependencyObject
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var t = child as T;
                if (t != null && ((UIElement)(object)t).IsVisible) return t;
                var found = FindFirst<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        // ---- Snackbar ----

        // template may contain {0}, which is rendered in bold (Flutter "<b>name</b>").
        public void ShowSnack(string template, string boldArg = null)
        {
            _snackLayer.Children.Clear();
            var tb = new TextBlock { Foreground = C.SnackText, FontSize = 14, TextWrapping = TextWrapping.Wrap };
            if (boldArg != null && template.Contains("{0}"))
            {
                int i = template.IndexOf("{0}", StringComparison.Ordinal);
                tb.Inlines.Add(new System.Windows.Documents.Run(template.Substring(0, i)));
                tb.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(boldArg)));
                tb.Inlines.Add(new System.Windows.Documents.Run(template.Substring(i + 3)));
            }
            else
            {
                tb.Text = boldArg != null ? string.Format(template, boldArg) : template;
            }
            var snack = new Border
            {
                Background = C.SnackBackground,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(96, 0, 32, 32),
                MaxWidth = 600,
                MinWidth = 280,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = tb,
                Effect = UI.Shadow(3, 10, 0.25)
            };
            snack.MouseLeftButtonDown += delegate { _snackLayer.Children.Clear(); };
            _snackLayer.Children.Add(snack);
            snack.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));

            if (_snackTimer != null) _snackTimer.Stop();
            _snackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _snackTimer.Tick += delegate
            {
                _snackTimer.Stop();
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
                fade.Completed += delegate { _snackLayer.Children.Remove(snack); };
                snack.BeginAnimation(OpacityProperty, fade);
            };
            _snackTimer.Start();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (AllowClose) return;
            // Like the desktop Flutter client: closing the window keeps the app in the tray.
            e.Cancel = true;
            Hide();
            App.I.OnWindowHidden();
        }
    }

    class DialogAction
    {
        public string Text;
        public Func<bool> Handler;
        public bool Danger;
        public bool IsDefault;
        public bool IsCancel;

        public DialogAction(string text, Func<bool> handler)
        {
            Text = text;
            Handler = handler;
        }

        public static DialogAction Cancel(string text = null)
        {
            return new DialogAction(text ?? L.Cancel, delegate { return true; }) { IsCancel = true };
        }
    }

    // Scaffold with a CustomAppBar; pages build their body via Build().
    abstract class Page : Grid
    {
        protected readonly MainWindow Main;
        readonly Grid _bodyHost = new Grid();
        readonly Grid _appBar = new Grid { Height = 64 };
        readonly Border _bottom = new Border();
        readonly Grid _overlay = new Grid();

        protected Page(MainWindow main)
        {
            Main = main;
            Background = C.Background;
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Children.Add(_appBar);
            SetRow(_bodyHost, 1);
            Children.Add(_bodyHost);
            SetRow(_overlay, 1);
            Children.Add(_overlay);
            SetRow(_bottom, 2);
            Children.Add(_bottom);
        }

        public virtual bool IsDirty { get { return false; } }
        public virtual void OnShown() { }
        public virtual void OnRemoved() { }

        protected void SetAppBar(string title, Geometry leading, params FrameworkElement[] actions)
        {
            _appBar.Children.Clear();
            _appBar.ColumnDefinitions.Clear();
            _appBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _appBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _appBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (leading != null)
            {
                var back = UI.IconButton(leading, null, delegate { Main.Pop(); });
                back.Margin = new Thickness(12, 0, 0, 0);
                back.VerticalAlignment = VerticalAlignment.Center;
                _appBar.Children.Add(back);
            }

            var tb = UI.TitleLarge(title);
            tb.TextWrapping = TextWrapping.NoWrap;
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            tb.VerticalAlignment = VerticalAlignment.Center;
            // centerTitle = !showBackButton (CustomAppBar)
            tb.HorizontalAlignment = leading == null ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            tb.Margin = new Thickness(leading == null ? 16 : 12, 0, 16, 0);
            SetColumn(tb, 1);
            if (leading == null) { SetColumnSpan(tb, 3); }
            _appBar.Children.Add(tb);

            var right = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            foreach (var a in actions) if (a != null) right.Children.Add(a);
            SetColumn(right, 2);
            _appBar.Children.Add(right);
        }

        protected void SetBody(UIElement body)
        {
            _bodyHost.Children.Clear();
            if (body != null) _bodyHost.Children.Add(body);
        }

        // Bottom section with a divider and a right-aligned submit button (RoutingDetailsSubmitButtonSection).
        protected void SetBottom(UIElement content)
        {
            if (content == null) { _bottom.Child = null; return; }
            var panel = new DockPanel();
            var div = UI.Divider();
            DockPanel.SetDock(div, Dock.Top);
            panel.Children.Add(div);
            var host = new Border { Padding = new Thickness(16), Child = content };
            panel.Children.Add(host);
            _bottom.Child = panel;
        }

        protected void SetFab(string label, Geometry icon, Action click)
        {
            _overlay.Children.Clear();
            if (label == null) return;
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = UI.Icon(icon, 24, C.White);
            ic.Margin = new Thickness(0, 0, 12, 0);
            ic.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(ic);
            sp.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 14, FontWeight = FontWeights.SemiBold });
            var fab = new Button
            {
                Style = UI.S("Fab"),
                Content = sp,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(16),
                Effect = UI.Shadow(3, 10, 0.3)
            };
            fab.Click += delegate { click(); };
            _overlay.Children.Add(fab);
            // Let clicks pass through the overlay except for the FAB itself.
            _overlay.IsHitTestVisible = true;
            _overlay.Background = null;
        }

        // DefaultPage.responsive: picture, title, description and an optional button, centered.
        protected static FrameworkElement Placeholder(Geometry icon, string title, string description, string button, Action click)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420, Margin = new Thickness(24) };
            var circle = new Border
            {
                Width = 120,
                Height = 120,
                CornerRadius = new CornerRadius(60),
                Background = C.BackgroundAdditional,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = UI.Icon(icon, 56, C.AccentAdditional),
                Margin = new Thickness(0, 0, 0, 24)
            };
            var ic = (FrameworkElement)circle.Child;
            ic.HorizontalAlignment = HorizontalAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(circle);
            var t = UI.Text(title, 24);
            t.TextAlignment = TextAlignment.Center;
            t.Margin = new Thickness(0, 0, 0, 8);
            sp.Children.Add(t);
            var d = UI.BodyMedium(description, C.NeutralDark);
            d.TextAlignment = TextAlignment.Center;
            sp.Children.Add(d);
            if (button != null)
            {
                var b = UI.Filled(button, click);
                b.HorizontalAlignment = HorizontalAlignment.Center;
                b.Margin = new Thickness(0, 24, 0, 0);
                sp.Children.Add(b);
            }
            return sp;
        }
    }

    // CustomTextField: label, outlined input, helper/error line.
    class FormField : StackPanel
    {
        public readonly TextBox Box;
        readonly PasswordBox _password;
        readonly TextBlock _helper;
        readonly TextBlock _hint;
        readonly string _helperText;
        public event Action Changed;

        public FormField(string label, string hint = null, string helper = null, int lines = 1, bool password = false)
        {
            Margin = new Thickness(0, 0, 0, 20);
            if (label != null)
            {
                var lb = UI.Text(label, 13, FontWeights.SemiBold, C.NeutralDark);
                lb.Margin = new Thickness(0, 0, 0, 6);
                Children.Add(lb);
            }

            var grid = new Grid();
            Box = new TextBox { Style = UI.S("TextBoxField") };
            if (lines > 1)
            {
                Box.AcceptsReturn = true;
                Box.TextWrapping = TextWrapping.Wrap;
                Box.VerticalContentAlignment = VerticalAlignment.Top;
                Box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                Box.MinHeight = 20 + lines * 19;
            }
            grid.Children.Add(Box);

            if (password)
            {
                _password = new PasswordBox { Style = UI.S("PasswordBoxField"), Padding = new Thickness(12, 10, 48, 10) };
                Box.Padding = new Thickness(12, 10, 48, 10);
                Box.Visibility = Visibility.Collapsed;
                grid.Children.Add(_password);
                Button eye = null;
                eye = UI.IconButton(Icons.Visibility, null, delegate
                {
                    bool show = Box.Visibility != Visibility.Visible;
                    if (show) Box.Text = _password.Password; else _password.Password = Box.Text;
                    Box.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                    _password.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                    eye.Content = UI.Icon(show ? Icons.VisibilityOff : Icons.Visibility, 22, C.NeutralDark);
                }, C.NeutralDark);
                eye.Content = UI.Icon(Icons.Visibility, 22, C.NeutralDark);
                eye.Height = 36;
                eye.Width = 36;
                eye.HorizontalAlignment = HorizontalAlignment.Right;
                eye.Margin = new Thickness(0, 0, 6, 0);
                eye.Focusable = false;
                grid.Children.Add(eye);
                _password.PasswordChanged += delegate { UpdateHint(); RaiseChanged(); };
            }

            _hint = new TextBlock
            {
                Text = hint ?? "",
                Foreground = C.NeutralLight,
                FontSize = 14,
                Margin = new Thickness(14, 10, 14, 10),
                IsHitTestVisible = false,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = lines > 1 ? VerticalAlignment.Top : VerticalAlignment.Center
            };
            grid.Children.Add(_hint);
            Children.Add(grid);

            _helperText = helper;
            _helper = UI.BodySmall(helper ?? "");
            _helper.Margin = new Thickness(14, 4, 14, 0);
            _helper.Visibility = helper == null ? Visibility.Collapsed : Visibility.Visible;
            Children.Add(_helper);

            Box.TextChanged += delegate { UpdateHint(); RaiseChanged(); };
            UpdateHint();
        }

        void RaiseChanged()
        {
            if (Changed != null) Changed();
        }

        void UpdateHint()
        {
            _hint.Visibility = string.IsNullOrEmpty(Value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public string Value
        {
            get
            {
                if (_password != null && _password.Visibility == Visibility.Visible) return _password.Password;
                return Box.Text;
            }
            set
            {
                Box.Text = value ?? "";
                if (_password != null) _password.Password = value ?? "";
                UpdateHint();
            }
        }

        public string Error
        {
            set
            {
                bool has = !string.IsNullOrEmpty(value);
                Box.Tag = has ? "error" : null;
                if (_password != null) _password.Tag = has ? "error" : null;
                _helper.Text = has ? value : (_helperText ?? "");
                _helper.Foreground = has ? C.Error : C.NeutralDark;
                _helper.Visibility = has || _helperText != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void FocusInput()
        {
            if (_password != null && _password.Visibility == Visibility.Visible) _password.Focus();
            else Box.Focus();
        }
    }

    // CustomDropdownMenu analogue.
    class DropdownField : StackPanel
    {
        readonly Button _button;
        readonly TextBlock _text;
        List<KeyValuePair<string, string>> _items = new List<KeyValuePair<string, string>>();
        public string SelectedKey { get; private set; }
        public event Action Changed;

        public DropdownField(string label)
        {
            Margin = new Thickness(0, 0, 0, 20);
            var lb = UI.Text(label, 13, FontWeights.SemiBold, C.NeutralDark);
            lb.Margin = new Thickness(0, 0, 0, 6);
            Children.Add(lb);

            var row = new DockPanel { LastChildFill = true };
            var arrow = UI.Icon(Icons.ArrowDropDown, 24, C.NeutralBlack);
            DockPanel.SetDock(arrow, Dock.Right);
            row.Children.Add(arrow);
            _text = UI.BodyMedium("");
            _text.VerticalAlignment = VerticalAlignment.Center;
            _text.TextTrimming = TextTrimming.CharacterEllipsis;
            _text.TextWrapping = TextWrapping.NoWrap;
            row.Children.Add(_text);
            _button = new Button { Style = UI.S("FieldButton"), Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch, Height = 42, Padding = new Thickness(12, 0, 8, 0) };
            _button.Click += delegate { Open(); };
            Children.Add(_button);
        }

        public void SetItems(IEnumerable<KeyValuePair<string, string>> items, string selected)
        {
            _items = items.ToList();
            Select(selected, false);
        }

        void Select(string key, bool notify)
        {
            var item = _items.FirstOrDefault(i => i.Key == key);
            if (item.Key == null && _items.Count > 0) item = _items[0];
            SelectedKey = item.Key;
            _text.Text = item.Value ?? "";
            if (notify && Changed != null) Changed();
        }

        void Open()
        {
            var entries = _items.Select(i =>
            {
                var key = i.Key;
                return new MenuEntry(i.Value, null, delegate { Select(key, true); }) { Selected = key == SelectedKey };
            }).ToList();
            UI.ShowMenu(_button, entries, _button.ActualWidth);
        }
    }

    // List tile pieces.
    static class Tiles
    {
        // CustomListTileSeparated: tappable title/subtitle, vertical divider, trailing widget.
        public static FrameworkElement Separated(string title, string subtitle, Action tap, FrameworkElement trailing, bool titleSmall = true)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texts = new StackPanel { Margin = new Thickness(16, 18, 16, 18), VerticalAlignment = VerticalAlignment.Center };
            var t = titleSmall ? UI.TitleSmall(title) : UI.BodyLarge(title);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.TextWrapping = TextWrapping.NoWrap;
            texts.Children.Add(t);
            if (subtitle != null)
            {
                var s = UI.BodyMedium(subtitle, C.NeutralLight);
                s.Margin = new Thickness(0, 2, 0, 0);
                s.TextTrimming = TextTrimming.CharacterEllipsis;
                s.TextWrapping = TextWrapping.NoWrap;
                texts.Children.Add(s);
            }
            var row = new Button { Style = UI.S("ListRow"), Content = texts, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            row.Click += delegate { tap(); };
            grid.Children.Add(row);

            var div = UI.VerticalDivider();
            Grid.SetColumn(div, 1);
            grid.Children.Add(div);

            if (trailing != null)
            {
                trailing.Margin = new Thickness(16);
                trailing.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(trailing, 2);
                grid.Children.Add(trailing);
            }
            return grid;
        }

        // CustomArrowListTile.
        public static FrameworkElement Arrow(string title, Action tap, Geometry trailingIcon = null)
        {
            var grid = new Grid { Margin = new Thickness(16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = UI.BodyLarge(title);
            t.Margin = new Thickness(0, 2, 16, 2);
            t.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(t);
            var ic = UI.Icon(trailingIcon ?? Icons.NavigateNext, 24, C.NeutralDark);
            ic.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ic, 1);
            grid.Children.Add(ic);
            var row = new Button { Style = UI.S("ListRow"), Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            row.Click += delegate { tap(); };
            return row;
        }

        // CustomSwitchTile.
        public static FrameworkElement Switch(string title, string description, bool value, Action<bool> changed, out ToggleButton toggle)
        {
            var grid = new Grid { Margin = new Thickness(16, 12, 16, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            texts.Children.Add(UI.BodyLarge(title));
            if (description != null)
            {
                var d = UI.BodyMedium(description, C.NeutralDark);
                d.Margin = new Thickness(0, 2, 0, 0);
                texts.Children.Add(d);
            }
            grid.Children.Add(texts);
            var tg = new ToggleButton { Style = UI.S("Switch"), IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
            tg.Click += delegate { changed(tg.IsChecked == true); };
            Grid.SetColumn(tg, 1);
            grid.Children.Add(tg);
            var row = new Button { Style = UI.S("ListRow"), Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch, Focusable = false };
            row.Click += delegate
            {
                if (!tg.IsEnabled) return;
                tg.IsChecked = tg.IsChecked != true;
                changed(tg.IsChecked == true);
            };
            toggle = tg;
            return row;
        }

        // CustomRadioListTile.
        public static RadioButton Radio(string title, string description, string group, bool isChecked, Action selected)
        {
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
            texts.Children.Add(UI.BodyLarge(title));
            if (description != null)
            {
                var d = UI.BodyMedium(description, C.NeutralDark);
                d.Margin = new Thickness(0, 2, 0, 0);
                texts.Children.Add(d);
            }
            var rb = new RadioButton { Style = UI.S("Radio"), Content = texts, GroupName = group, IsChecked = isChecked, Margin = new Thickness(8, 0, 16, 0) };
            rb.Checked += delegate { selected(); };
            return rb;
        }

        public static TextBlock Section(string title)
        {
            var t = UI.Text(title, 14, FontWeights.SemiBold, C.AccentText);
            t.Margin = new Thickness(16, 20, 16, 4);
            return t;
        }
    }
}
