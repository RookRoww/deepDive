namespace DeepDive;

internal static class DashboardStyle
{
    public static readonly Color Background = Color.FromArgb(243, 246, 251);
    public static readonly Color Text = Color.FromArgb(23, 38, 66);
    public static readonly Color SecondaryText = Color.FromArgb(71, 85, 105);
    public static readonly Color Blue = Color.FromArgb(37, 99, 235);
    public static readonly Color Border = Color.FromArgb(215, 223, 235);

    public static Button Button(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text, AutoSize = true, MinimumSize = new Size(0, 34),
            Margin = new Padding(0, 0, 6, 0), Padding = new Padding(8, 1, 8, 1),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? Blue : Color.White,
            ForeColor = primary ? Color.White : Text, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? Blue : Border;
        return button;
    }
}

internal sealed class SpaceCard : UserControl
{
    private readonly TableLayoutPanel _layout;
    private readonly Button _detailsButton;
    private readonly ListView _details;
    private readonly int _detailsHeight;
    private readonly int _baseHeight;
    private readonly int _lineHeight;
    private readonly int _itemCount;

    public int SpaceId { get; }
    public bool Expanded { get; private set; }

    public event EventHandler? LaunchRequested;
    public event EventHandler? EditRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? UpRequested;
    public event EventHandler? DownRequested;

    public SpaceCard(Space space, bool expanded, bool canMoveUp, bool canMoveDown)
    {
        SpaceId = space.Id;
        _itemCount = space.Items.Count;
        Margin = new Padding(0, 0, 0, 10);
        Padding = new Padding(12, 10, 12, 10);
        BackColor = Color.White;
        ForeColor = DashboardStyle.Text;
        BorderStyle = BorderStyle.FixedSingle;
        AccessibleName = space.Name;
        AutoScaleMode = AutoScaleMode.Inherit;

        _lineHeight = Math.Max(19, TextRenderer.MeasureText("Ag", Font).Height + 1);
        var headerHeight = Math.Max(45, _lineHeight * 2 + 5);
        var buttonsHeight = Math.Max(39, _lineHeight + 15);
        _baseHeight = Padding.Vertical + headerHeight + buttonsHeight + 2;
        _detailsHeight = Math.Clamp(_itemCount + 1, 3, 7) * _lineHeight + 11;

        _layout = new TableLayoutPanel
        {
            ColumnCount = 1, RowCount = 3, Dock = DockStyle.Fill, Margin = Padding.Empty
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, headerHeight));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonsHeight));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

        var header = new TableLayoutPanel
        {
            ColumnCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var tile = new Label
        {
            Text = IconText(space.Icon), AutoSize = false, Width = 38, Height = 35,
            BackColor = WorkspaceColor(space.Color), ForeColor = Color.White,
            Font = new Font("Segoe UI Symbol", 13F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter, Margin = Padding.Empty,
            Anchor = AnchorStyles.Left, AccessibleName = $"{space.Icon} icon"
        };
        var title = new Label
        {
            Text = space.Name, Dock = DockStyle.Fill, AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold), Margin = Padding.Empty
        };
        header.Controls.Add(tile, 0, 0);
        header.Controls.Add(title, 1, 0);
        _layout.Controls.Add(header, 0, 0);

        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Dock = DockStyle.Fill, Margin = Padding.Empty, TabStop = false
        };
        actions.Controls.Add(CreateButton("Launch", (_, _) => LaunchRequested?.Invoke(this, EventArgs.Empty), primary: true));
        actions.Controls.Add(CreateButton("Edit", (_, _) => EditRequested?.Invoke(this, EventArgs.Empty)));
        _detailsButton = CreateButton("Show items", (_, _) => SetExpanded(!Expanded));
        actions.Controls.Add(_detailsButton);
        actions.Controls.Add(CreateButton("Move up", (_, _) => UpRequested?.Invoke(this, EventArgs.Empty), enabled: canMoveUp));
        actions.Controls.Add(CreateButton("Move down", (_, _) => DownRequested?.Invoke(this, EventArgs.Empty), enabled: canMoveDown));
        actions.Controls.Add(CreateButton("Delete", (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty)));
        _layout.Controls.Add(actions, 0, 1);

        _details = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable,
            MultiSelect = false, Margin = new Padding(0, 4, 0, 0), BackColor = Color.White,
            ForeColor = DashboardStyle.Text, AccessibleName = "Workspace items"
        };
        _details.Columns.Add("Item");
        _details.Columns.Add("Type");
        _details.Columns.Add("Path or web address");
        _details.Columns.Add("Status");
        foreach (var item in space.Items.OrderBy(item => item.Order))
            _details.Items.Add(new ListViewItem([item.Name, item.Type.Name, item.Target, item.Enabled ? "Enabled" : "Disabled"]));
        if (_itemCount == 0)
            _details.Items.Add(new ListViewItem("No items yet"));
        _details.ClientSizeChanged += (_, _) => FitColumns();
        _layout.Controls.Add(_details, 0, 2);

        Controls.Add(_layout);
        SetExpanded(expanded);
    }

    private static Button CreateButton(string text, EventHandler onClick, bool enabled = true, bool primary = false)
    {
        var button = DashboardStyle.Button(text, primary);
        button.Enabled = enabled;
        button.Click += onClick;
        return button;
    }

    private void SetExpanded(bool expanded)
    {
        Expanded = expanded;
        _detailsButton.Text = expanded ? "Hide items" : $"Show items ({_itemCount})";
        _details.Visible = expanded;
        _layout.RowStyles[2].Height = expanded ? _detailsHeight : 0;
        Height = _baseHeight + (expanded ? _detailsHeight : 0);
        if (expanded)
            FitColumns();
    }

    private void FitColumns()
    {
        if (_details.Columns.Count != 4)
            return;

        var width = _details.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
        if (width < 120)
            return;

        _details.Columns[0].Width = width * 27 / 100;
        _details.Columns[1].Width = width * 16 / 100;
        _details.Columns[3].Width = width * 16 / 100;
        _details.Columns[2].Width = width - _details.Columns[0].Width - _details.Columns[1].Width - _details.Columns[3].Width;
    }

    private static Color WorkspaceColor(string? hex)
    {
        if (hex is { Length: 7 } && hex[0] == '#' &&
            int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        return DashboardStyle.Blue;
    }

    private static string IconText(string? icon) => icon?.Trim().ToUpperInvariant() switch
    {
        "BRIEFCASE" => "▣",
        "FOLDER" => "▰",
        "BOOK" => "▤",
        "GLOBE" => "◎",
        "STAR" => "★",
        "CODE" => "‹›",
        "CAMERA" => "◉",
        "HEART" => "♥",
        "SCHOOL" => "✎",
        _ => "◆"
    };
}
