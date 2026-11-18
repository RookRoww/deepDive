namespace DeepDive;

internal sealed class ResultForm : Form
{
    public int? EditItemId { get; private set; }

    public ResultForm(Space space, IReadOnlyList<Result> results, string? saveWarning)
    {
        Text = "Launch results";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(780, 475);
        MinimumSize = new Size(625, 370);
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 10F);
        BackColor = DashboardStyle.Background;
        ForeColor = DashboardStyle.Text;

        var content = new TableLayoutPanel
        {
            ColumnCount = 1, RowCount = 5, Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 12)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var name = new Label
        {
            Text = space.Name, UseMnemonic = false, AutoSize = true,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 4)
        };
        content.Controls.Add(name, 0, 0);
        var fixes = results.Count(result => result.NeedsFix);
        var sent = results.Count - fixes;
        var summary = new Label
        {
            Text = results.Count == 0
                ? "This workspace has no enabled items to launch."
                : $"{sent} {(sent == 1 ? "request" : "requests")} sent  •  {fixes} {(fixes == 1 ? "item needs" : "items need")} attention",
            AutoSize = true, ForeColor = DashboardStyle.SecondaryText,
            Margin = new Padding(0, 0, 0, 10)
        };
        content.Controls.Add(summary, 0, 1);

        if (saveWarning is not null)
        {
            var warning = new Label
            {
                Text = saveWarning, AutoSize = true, UseMnemonic = false,
                BackColor = Color.FromArgb(255, 239, 210), ForeColor = Color.FromArgb(105, 64, 18),
                Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 0, 0, 10)
            };
            content.SizeChanged += (_, _) =>
                warning.MaximumSize = new Size(Math.Max(100, content.ClientSize.Width - content.Padding.Horizontal), 0);
            content.Controls.Add(warning, 0, 2);
        }

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, BackgroundColor = Color.White, ForeColor = DashboardStyle.Text,
            BorderStyle = BorderStyle.FixedSingle, GridColor = DashboardStyle.Border, ReadOnly = true,
            RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            Margin = Padding.Empty, AccessibleName = "Launch results"
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Item", FillWeight = 29, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", FillWeight = 25, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Details", FillWeight = 46, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.DefaultCellStyle.Padding = new Padding(5, 4, 5, 4);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 254);
        var names = space.Items.ToDictionary(item => item.Id, item => item.Name);
        var firstIssue = -1;
        foreach (var result in results)
        {
            var itemName = names.GetValueOrDefault(result.ItemId, "Workspace item");
            var index = grid.Rows.Add(itemName, result.NeedsFix ? "Needs attention" : "Request sent", result.Message);
            grid.Rows[index].Tag = result;
            if (result.NeedsFix && firstIssue < 0)
                firstIssue = index;
        }
        content.Controls.Add(grid, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, WrapContents = false,
            AutoSize = true, Margin = new Padding(0, 12, 0, 0)
        };
        var close = DashboardStyle.Button("Close");
        close.DialogResult = DialogResult.OK;
        CancelButton = close;
        var edit = DashboardStyle.Button("Edit workspace", true);
        var repair = DashboardStyle.Button("Edit selected item");
        edit.Click += (_, _) => DialogResult = DialogResult.Retry;
        repair.Click += (_, _) =>
        {
            if (grid.CurrentRow?.Tag is Result { NeedsFix: true } result)
            {
                EditItemId = result.ItemId;
                DialogResult = DialogResult.Retry;
            }
        };
        void UpdateRepair() => repair.Enabled = grid.CurrentRow?.Tag is Result { NeedsFix: true };
        grid.SelectionChanged += (_, _) => UpdateRepair();
        Shown += (_, _) =>
        {
            if (firstIssue >= 0)
                grid.CurrentCell = grid.Rows[firstIssue].Cells[0];
            UpdateRepair();
        };
        UpdateRepair();
        buttons.Controls.Add(close);
        buttons.Controls.Add(edit);
        buttons.Controls.Add(repair);
        content.Controls.Add(buttons, 0, 4);
        Controls.Add(content);
    }
}
