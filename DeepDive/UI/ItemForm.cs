namespace DeepDive;

internal sealed class ItemForm : Form
{
    private readonly ComboBox typeBox = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly TextBox nameBox = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly TextBox targetBox = new() { Dock = DockStyle.Fill, MaxLength = 1000 };
    private readonly Button browse = EditorUi.Button("Browse");
    private readonly CheckBox enabledBox = new()
    {
        AutoSize = true,
        Text = "Open this action when the workspace launches",
        Margin = new Padding(2, 11, 0, 8),
    };
    private readonly Label guidance = EditorUi.Note("");
    private readonly Label feedback = new()
    {
        AutoSize = true,
        ForeColor = EditorUi.Error,
        Margin = new Padding(2, 5, 0, 0),
    };

    public Item Edited { get; private set; }

    public ItemForm(Item item, IReadOnlyList<ItemType> types)
    {
        Edited = item;
        Text = string.IsNullOrWhiteSpace(item.Name) ? "Add action" : "Edit action";
        EditorUi.Configure(this, new Size(620, 386), new Size(590, 405), resizable: false);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 18),
            ColumnCount = 1,
            RowCount = 5,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(EditorUi.Heading(Text), 0, 0);
        root.Controls.Add(EditorUi.Note("Set the type and location Deep Dive should open."), 0, 1);
        root.Controls.Add(BuildFields(), 0, 2);

        feedback.Visible = false;
        feedback.AccessibleName = "Action validation message";
        feedback.MaximumSize = new Size(ClientSize.Width - 48, 0);
        root.Controls.Add(feedback, 0, 3);

        var footer = EditorUi.Footer(out var cancel, out var save, "Save action");
        root.Controls.Add(footer, 0, 4);
        CancelButton = cancel;
        AcceptButton = save;
        save.Click += (_, _) => SaveAction();

        typeBox.DisplayMember = nameof(ItemType.Name);
        typeBox.DataSource = types.ToList();
        typeBox.SelectedItem = types.FirstOrDefault(type => type.Id == item.TypeId);
        if (typeBox.SelectedIndex < 0 && typeBox.Items.Count > 0)
            typeBox.SelectedIndex = 0;

        nameBox.Text = item.Name;
        targetBox.Text = item.Target;
        enabledBox.Checked = item.Enabled;
        typeBox.SelectedIndexChanged += (_, _) => UpdateTargetMode();
        browse.Click += (_, _) => BrowseTarget();
        UpdateTargetMode();
        Shown += (_, _) =>
        {
            nameBox.Focus();
            nameBox.SelectAll();
        };
    }

    private TableLayoutPanel BuildFields()
    {
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 13, 0, 0),
            ColumnCount = 2,
            RowCount = 8,
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var row = 0; row < fields.RowCount; ++row)
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var typeLabel = EditorUi.FieldLabel("Type");
        fields.Controls.Add(typeLabel, 0, 0);
        fields.SetColumnSpan(typeLabel, 2);
        fields.Controls.Add(typeBox, 0, 1);
        fields.SetColumnSpan(typeBox, 2);
        var nameLabel = EditorUi.FieldLabel("Name");
        nameLabel.Margin = new Padding(2, 7, 0, 3);
        fields.Controls.Add(nameLabel, 0, 2);
        fields.SetColumnSpan(nameLabel, 2);
        fields.Controls.Add(nameBox, 0, 3);
        fields.SetColumnSpan(nameBox, 2);
        var targetLabel = EditorUi.FieldLabel("Target");
        targetLabel.Margin = new Padding(2, 7, 0, 3);
        fields.Controls.Add(targetLabel, 0, 4);
        fields.SetColumnSpan(targetLabel, 2);
        fields.Controls.Add(targetBox, 0, 5);
        targetBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        browse.Margin = new Padding(5, 0, 0, 0);
        fields.Controls.Add(browse, 1, 5);
        guidance.Margin = new Padding(2, 3, 0, 0);
        fields.Controls.Add(guidance, 0, 6);
        fields.SetColumnSpan(guidance, 2);
        fields.Controls.Add(enabledBox, 0, 7);
        fields.SetColumnSpan(enabledBox, 2);

        typeBox.AccessibleName = "Action type";
        nameBox.AccessibleName = "Action name";
        targetBox.AccessibleName = "Action target";
        browse.AccessibleName = "Browse for local target";
        return fields;
    }

    private string SelectedTypeCode => typeBox.SelectedItem is ItemType type ? ItemRules.Code(type) : "";

    private void UpdateTargetMode()
    {
        browse.Enabled = SelectedTypeCode is "APP" or "FOLDER";
        guidance.Text = SelectedTypeCode switch
        {
            "APP" => "Select a local .exe file.",
            "FOLDER" => "Select a local folder.",
            "WEBSITE" => "Enter a full web address starting with https:// or http://.",
            _ => "Select the type of resource to open.",
        };
    }

    private void BrowseTarget()
    {
        if (SelectedTypeCode == "APP")
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select an application",
                Filter = "Applications (*.exe)|*.exe",
                CheckFileExists = true,
                CheckPathExists = true,
            };
            if (File.Exists(targetBox.Text))
                dialog.FileName = targetBox.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK)
                targetBox.Text = dialog.FileName;
        }
        else if (SelectedTypeCode == "FOLDER")
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            };
            if (Directory.Exists(targetBox.Text))
                dialog.SelectedPath = targetBox.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK)
                targetBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveAction()
    {
        if (typeBox.SelectedItem is not ItemType type)
        {
            ShowValidation("Choose an action type.");
            return;
        }

        var item = new Item
        {
            Id = Edited.Id,
            SpaceId = Edited.SpaceId,
            Order = Edited.Order,
            TypeId = type.Id,
            Type = type,
            Name = nameBox.Text.Trim(),
            Target = targetBox.Text.Trim(),
            Enabled = enabledBox.Checked,
        };
        var error = ItemRules.Validate(item);
        if (error is not null)
        {
            ShowValidation(error);
            return;
        }

        Edited = item;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowValidation(string message)
    {
        feedback.Text = message;
        feedback.Visible = true;
    }
}
