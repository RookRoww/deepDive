namespace DeepDive;

public sealed class SpaceForm : Form
{
    private static readonly (string Label, string Value)[] Colors =
    [
        ("Ocean blue", "#2563EB"),
        ("Forest green", "#16A34A"),
        ("Violet", "#7C3AED"),
        ("Amber", "#D97706"),
        ("Red", "#DC2626"),
        ("Slate", "#475569"),
    ];

    private static readonly string[] Icons =
        ["Briefcase", "Folder", "Book", "Globe", "Star", "Code", "Camera"];

    private readonly IReadOnlyList<ItemType> types;
    private readonly Func<Space, string?> validate;
    private readonly TextBox nameBox = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly ComboBox colorBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox iconBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Panel colorSample = new() { Width = 20, Height = 20, Margin = new Padding(8, 4, 0, 0) };
    private readonly ListView actions = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        ShowItemToolTips = true,
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(0, 4, 0, 8),
    };
    private readonly Button edit = EditorUi.Button("Edit");
    private readonly Button remove = EditorUi.Button("Remove");
    private readonly Button up = EditorUi.Button("Move up");
    private readonly Button down = EditorUi.Button("Move down");
    private readonly Label feedback = new()
    {
        AutoSize = true,
        ForeColor = EditorUi.Error,
        Margin = new Padding(0, 6, 0, 4),
    };

    public Space Draft { get; }

    public SpaceForm(
        Space draft,
        IReadOnlyList<ItemType> types,
        Func<Space, string?> validate,
        int? focusItemId = null)
    {
        Draft = draft;
        this.types = types.Where(ItemRules.IsSupported).ToList();
        this.validate = validate;

        Text = Draft.Id == 0 ? "New workspace" : "Edit workspace";
        EditorUi.Configure(this, new Size(780, 545), new Size(670, 510), resizable: true);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 18),
            ColumnCount = 1,
            RowCount = 8,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var heading = EditorUi.Heading(Text);
        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(EditorUi.Note("Choose what opens when you launch this workspace."), 0, 1);
        root.Controls.Add(BuildAppearance(), 0, 2);

        var actionHeading = new Label
        {
            Text = "Actions",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 17, 0, 0),
        };
        root.Controls.Add(actionHeading, 0, 3);
        actions.Columns.Add("Order", 58);
        actions.Columns.Add("Name", 146);
        actions.Columns.Add("Type", 104);
        actions.Columns.Add("Target", 297);
        actions.Columns.Add("Enabled", 72);
        actions.AccessibleName = "Workspace actions in launch order";
        actions.SelectedIndexChanged += (_, _) => UpdateActionButtons();
        actions.ClientSizeChanged += (_, _) => FitTargetColumn();
        actions.DoubleClick += (_, _) => EditAction();
        root.Controls.Add(actions, 0, 4);

        var actionButtons = EditorUi.ButtonRow();
        var add = EditorUi.Button("Add action");
        add.Enabled = this.types.Count != 0;
        add.Click += (_, _) => AddAction();
        edit.Click += (_, _) => EditAction();
        remove.Click += (_, _) => RemoveAction();
        up.Click += (_, _) => MoveAction(-1);
        down.Click += (_, _) => MoveAction(1);
        actionButtons.Controls.AddRange([add, edit, remove, up, down]);
        root.Controls.Add(actionButtons, 0, 5);

        feedback.Visible = false;
        feedback.AccessibleName = "Workspace validation message";
        root.Controls.Add(feedback, 0, 6);

        var footer = EditorUi.Footer(out var cancel, out var save, "Save workspace");
        root.Controls.Add(footer, 0, 7);
        CancelButton = cancel;
        AcceptButton = save;
        save.Click += (_, _) => SaveDraft();

        nameBox.Text = Draft.Name;
        LoadAppearance();
        RefreshActions();
        Shown += (_, _) =>
        {
            FitTargetColumn();
            if (focusItemId is int id)
            {
                var action = Draft.Items.Find(item => item.Id == id);
                if (action is not null)
                {
                    RefreshActions(action);
                    actions.Focus();
                    return;
                }
            }
            nameBox.Focus();
            nameBox.SelectAll();
        };
    }

    private TableLayoutPanel BuildAppearance()
    {
        var appearance = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
            RowCount = 2,
            ColumnCount = 4,
        };
        appearance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51));
        appearance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        appearance.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        appearance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        appearance.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        appearance.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        appearance.Controls.Add(EditorUi.FieldLabel("Name"), 0, 0);
        appearance.Controls.Add(EditorUi.FieldLabel("Color"), 1, 0);
        appearance.Controls.Add(EditorUi.FieldLabel("Icon"), 3, 0);
        appearance.Controls.Add(nameBox, 0, 1);
        appearance.Controls.Add(colorBox, 1, 1);
        appearance.Controls.Add(colorSample, 2, 1);
        appearance.Controls.Add(iconBox, 3, 1);
        nameBox.AccessibleName = "Workspace name";
        colorBox.AccessibleName = "Workspace color";
        iconBox.AccessibleName = "Workspace icon";
        return appearance;
    }

    private void LoadAppearance()
    {
        var colors = Colors.Select(color => new ColorChoice(color.Label, color.Value)).ToList();
        if (!string.IsNullOrWhiteSpace(Draft.Color) &&
            !colors.Any(color => string.Equals(color.Value, Draft.Color, StringComparison.OrdinalIgnoreCase)))
        {
            colors.Insert(0, new ColorChoice($"Current ({Draft.Color})", Draft.Color));
        }

        colorBox.DataSource = colors;
        colorBox.DisplayMember = nameof(ColorChoice.Label);
        var selected = colors.FindIndex(color => string.Equals(color.Value, Draft.Color, StringComparison.OrdinalIgnoreCase));
        colorBox.SelectedIndex = Math.Max(0, selected);
        colorBox.SelectedIndexChanged += (_, _) => UpdateColorSample();
        UpdateColorSample();

        iconBox.Items.AddRange(Icons);
        if (!string.IsNullOrWhiteSpace(Draft.Icon) && !Icons.Contains(Draft.Icon, StringComparer.OrdinalIgnoreCase))
        {
            iconBox.Items.Insert(0, Draft.Icon);
        }
        var iconIndex = iconBox.Items.Cast<string>().ToList().FindIndex(
            icon => string.Equals(icon, Draft.Icon, StringComparison.OrdinalIgnoreCase));
        iconBox.SelectedIndex = Math.Max(0, iconIndex);
    }

    private void UpdateColorSample()
    {
        if (colorBox.SelectedItem is not ColorChoice choice)
            return;

        colorSample.BackColor = System.Drawing.ColorTranslator.FromHtml(
            choice.Value.Length == 7 && choice.Value[0] == '#' && choice.Value.AsSpan(1).ToString().All(Uri.IsHexDigit)
                ? choice.Value
                : Colors[0].Value);
    }

    private void SaveDraft()
    {
        Draft.Name = nameBox.Text.Trim();
        Draft.Color = ((ColorChoice)colorBox.SelectedItem!).Value;
        Draft.Icon = (string)iconBox.SelectedItem!;
        string? error;
        try
        {
            error = validate(Draft);
        }
        catch (Microsoft.Data.SqlClient.SqlException exception)
        {
            MessageBox.Show(this, $"Could not check the workspace. Try saving again or cancel.\n\n{exception.Message}",
                "Deep Dive", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (error is not null)
        {
            feedback.MaximumSize = new Size(Math.Max(200, ClientSize.Width - 48), 0);
            feedback.Text = error;
            feedback.Visible = true;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void AddAction()
    {
        if (types.Count == 0)
            return;

        var action = new Item
        {
            SpaceId = Draft.Id,
            TypeId = types[0].Id,
            Type = types[0],
            Order = Draft.Items.Count + 1,
            Enabled = true,
        };

        using var dialog = new ItemForm(action, types);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Draft.Items.Add(dialog.Edited);
            RefreshActions(dialog.Edited);
        }
    }

    private void EditAction()
    {
        var index = SelectedIndex;
        if (index < 0)
            return;

        using var dialog = new ItemForm(Draft.Items[index], types);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Draft.Items[index] = dialog.Edited;
            RefreshActions(dialog.Edited);
        }
    }

    private void RemoveAction()
    {
        var index = SelectedIndex;
        if (index < 0)
            return;

        Draft.Items.RemoveAt(index);
        RefreshActions(Draft.Items.Count == 0 ? null : Draft.Items[Math.Min(index, Draft.Items.Count - 1)]);
    }

    private void MoveAction(int direction)
    {
        var index = SelectedIndex;
        var destination = index + direction;
        if (index < 0 || destination < 0 || destination >= Draft.Items.Count)
            return;

        var action = Draft.Items[index];
        Draft.Items.RemoveAt(index);
        Draft.Items.Insert(destination, action);
        RefreshActions(action);
    }

    private int SelectedIndex => actions.SelectedIndices.Count == 0 ? -1 : actions.SelectedIndices[0];

    private void RefreshActions(Item? selected = null)
    {
        actions.BeginUpdate();
        try
        {
            actions.Items.Clear();
            for (var i = 0; i < Draft.Items.Count; ++i)
            {
                var action = Draft.Items[i];
                action.Order = i + 1;
                var row = new ListViewItem(action.Order.ToString())
                {
                    ToolTipText = action.Target,
                };
                row.SubItems.Add(action.Name);
                row.SubItems.Add(action.Type.Name);
                row.SubItems.Add(action.Target);
                row.SubItems.Add(action.Enabled ? "Yes" : "No");
                actions.Items.Add(row);
                if (ReferenceEquals(action, selected))
                {
                    row.Selected = true;
                    row.Focused = true;
                    row.EnsureVisible();
                }
            }
        }
        finally
        {
            actions.EndUpdate();
        }
        UpdateActionButtons();
    }

    private void FitTargetColumn()
    {
        if (actions.Columns.Count > 4)
        {
            var reserved = actions.Columns.Cast<ColumnHeader>().Where((_, index) => index != 3).Sum(column => column.Width);
            actions.Columns[3].Width = Math.Max(190, actions.ClientSize.Width - reserved - SystemInformation.VerticalScrollBarWidth);
        }
    }

    private void UpdateActionButtons()
    {
        var index = SelectedIndex;
        edit.Enabled = index >= 0 && types.Count != 0;
        remove.Enabled = index >= 0;
        up.Enabled = index > 0;
        down.Enabled = index >= 0 && index < Draft.Items.Count - 1;
    }

    private sealed record ColorChoice(string Label, string Value);
}

internal static class EditorUi
{
    internal static readonly Color Ink = Color.FromArgb(23, 38, 66);
    internal static readonly Color Muted = Color.FromArgb(71, 85, 105);
    internal static readonly Color Background = Color.FromArgb(243, 246, 251);
    internal static readonly Color Accent = Color.FromArgb(37, 99, 235);
    internal static readonly Color Error = Color.FromArgb(153, 27, 27);

    internal static void Configure(Form form, Size size, Size minimum, bool resizable)
    {
        form.Font = new Font("Segoe UI", 10);
        form.BackColor = Background;
        form.ForeColor = Ink;
        form.ClientSize = size;
        form.MinimumSize = minimum;
        form.StartPosition = FormStartPosition.CenterParent;
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.FormBorderStyle = resizable ? FormBorderStyle.Sizable : FormBorderStyle.FixedDialog;
        form.MinimizeBox = false;
        form.MaximizeBox = resizable;
        form.ShowInTaskbar = false;
    }

    internal static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 18, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 4),
    };

    internal static Label Note(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Muted,
        Margin = new Padding(0, 0, 0, 2),
    };

    internal static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(2, 0, 0, 3),
    };

    internal static Button Button(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(82, 34),
            Padding = new Padding(9, 2, 9, 2),
            Margin = new Padding(0, 0, 7, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Color.White,
            ForeColor = primary ? Color.White : Ink,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(203, 213, 225);
        return button;
    }

    internal static FlowLayoutPanel ButtonRow() => new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = false,
        Margin = Padding.Empty,
    };

    internal static FlowLayoutPanel Footer(out Button cancel, out Button save, string saveText)
    {
        var row = ButtonRow();
        row.FlowDirection = FlowDirection.RightToLeft;
        row.Margin = new Padding(0, 14, 0, 0);
        cancel = Button("Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        save = Button(saveText, primary: true);
        save.Margin = Padding.Empty;
        cancel.Margin = new Padding(0, 0, 8, 0);
        row.Controls.AddRange([save, cancel]);
        return row;
    }
}
