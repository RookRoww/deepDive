namespace DeepDive;

internal sealed class MainForm : Form
{
    private readonly IStore _store;
    private readonly SpaceService _spaces;
    private readonly Launcher _launcher = new();
    private readonly FlowLayoutPanel _cards;
    private readonly Button _newButton;
    private readonly Label _empty;
    private bool _busy;

    public MainForm(IStore store, string? sessionNotice)
    {
        _store = store;
        _spaces = new SpaceService(store);
        Text = "Deep Dive";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(970, 690);
        MinimumSize = new Size(700, 490);
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 10F);
        BackColor = DashboardStyle.Background;
        ForeColor = DashboardStyle.Text;

        var content = new TableLayoutPanel
        {
            ColumnCount = 1, RowCount = 3, Dock = DockStyle.Fill,
            Padding = new Padding(22, 16, 22, 16)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var heading = new Label
        {
            Text = "Your workspaces", Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            AutoSize = true, Margin = new Padding(0, 0, 8, 12)
        };
        _newButton = DashboardStyle.Button("New workspace", true);
        _newButton.Anchor = AnchorStyles.Right;
        _newButton.Margin = new Padding(0, 0, 0, 10);
        _newButton.Click += (_, _) => EditSpace(null);
        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(_newButton, 1, 0);
        content.Controls.Add(header, 0, 0);

        if (sessionNotice is not null)
        {
            var notice = new Label
            {
                Text = sessionNotice, AutoSize = true, UseMnemonic = false,
                BackColor = Color.FromArgb(255, 239, 210),
                ForeColor = Color.FromArgb(105, 64, 18),
                Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 0, 0, 10)
            };
            content.SizeChanged += (_, _) =>
                notice.MaximumSize = new Size(Math.Max(100, content.ClientSize.Width - content.Padding.Horizontal), 0);
            content.Controls.Add(notice, 0, 1);
        }

        _cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Margin = Padding.Empty, TabStop = false
        };
        _cards.ClientSizeChanged += (_, _) => SizeCards();
        content.Controls.Add(_cards, 0, 2);

        _empty = new Label
        {
            Text = "Create a workspace to group the apps, folders, and websites you open together.",
            AutoSize = true, ForeColor = DashboardStyle.SecondaryText, Margin = new Padding(0, 12, 0, 0)
        };
        Controls.Add(content);
        Shown += (_, _) => RefreshCards();
    }

    private void RefreshCards()
    {
        var expanded = _cards.Controls.OfType<SpaceCard>()
            .Where(card => card.Expanded)
            .Select(card => card.SpaceId)
            .ToHashSet();

        IReadOnlyList<Space> saved;
        try
        {
            saved = _store.GetSpaces();
        }
        catch (Exception ex)
        {
            ShowError("Could not load the saved workspaces.", ex);
            return;
        }

        var ordered = saved.OrderBy(space => space.Order).ThenBy(space => space.Id).ToArray();
        _cards.SuspendLayout();
        try
        {
            foreach (var old in _cards.Controls.OfType<SpaceCard>().ToArray())
                old.Dispose();
            _cards.Controls.Clear();

            if (ordered.Length == 0)
                _cards.Controls.Add(_empty);

            for (var index = 0; index < ordered.Length; index++)
            {
                var space = ordered[index];
                var position = index;
                var card = new SpaceCard(space, expanded.Contains(space.Id), position > 0, position < ordered.Length - 1);
                card.EditRequested += (_, _) => EditSpace(space);
                card.DeleteRequested += (_, _) => DeleteSpace(space);
                card.UpRequested += (_, _) => MoveSpace(ordered, position, position - 1);
                card.DownRequested += (_, _) => MoveSpace(ordered, position, position + 1);
                card.LaunchRequested += async (_, _) => await LaunchSpaceAsync(space);
                _cards.Controls.Add(card);
            }
            SizeCards();
        }
        finally
        {
            _cards.ResumeLayout();
        }
    }

    private void SizeCards()
    {
        var width = Math.Max(100, _cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
        foreach (var card in _cards.Controls.OfType<SpaceCard>())
            card.Width = width;
    }

    private void EditSpace(Space? space, int? focusItemId = null)
    {
        if (_busy)
            return;

        try
        {
            var draft = space is null ? _spaces.NewDraft() : _spaces.EditDraft(space);
            using var editor = new SpaceForm(draft, _store.GetTypes(), CheckDraft, focusItemId);
            while (editor.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    _spaces.Save(editor.Draft);
                    RefreshCards();
                    break;
                }
                catch (Exception ex)
                {
                    ShowError("Could not save the workspace. You can retry or close the editor.", ex);
                }
            }
        }
        catch (Exception ex)
        {
            ShowError("Could not open the workspace editor.", ex);
        }
    }

    private string? CheckDraft(Space draft)
    {
        try
        {
            return _spaces.Validate(draft);
        }
        catch (Exception ex)
        {
            return $"Could not validate the workspace. {ex.Message}";
        }
    }

    private void DeleteSpace(Space space)
    {
        if (_busy || MessageBox.Show(this, $"Delete the workspace “{space.Name}”?", "Delete workspace",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            _store.DeleteSpace(space.Id);
            RefreshCards();
        }
        catch (Exception ex)
        {
            ShowError("Could not delete the workspace.", ex);
        }
    }

    private void MoveSpace(Space[] visibleOrder, int from, int to)
    {
        if (_busy || to < 0 || to >= visibleOrder.Length)
            return;

        var ids = visibleOrder.Select(space => space.Id).ToArray();
        (ids[from], ids[to]) = (ids[to], ids[from]);
        try
        {
            _store.SaveOrder(ids);
            RefreshCards();
        }
        catch (Exception ex)
        {
            ShowError("Could not save the workspace order.", ex);
        }
    }

    private async Task LaunchSpaceAsync(Space space)
    {
        if (_busy)
            return;

        SetBusy(true);
        IReadOnlyList<Result> results;
        string? saveWarning = null;
        try
        {
            results = await Task.Run(() => _launcher.Launch(space));
            if (results.Count > 0)
            {
                try
                {
                    await Task.Run(() => _store.SaveResults(results));
                }
                catch (Exception ex)
                {
                    saveWarning = $"The launch results could not be saved. {ex.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            ShowError("Could not launch the workspace.", ex);
            return;
        }
        finally
        {
            SetBusy(false);
        }

        using var summary = new ResultForm(space, results, saveWarning);
        if (summary.ShowDialog(this) == DialogResult.Retry)
            EditSpace(space, summary.EditItemId);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _cards.Enabled = !busy;
        _newButton.Enabled = !busy;
        UseWaitCursor = busy;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (_busy && e.CloseReason == CloseReason.UserClosing)
            e.Cancel = true;
    }

    private void ShowError(string message, Exception exception) =>
        MessageBox.Show(this, $"{message}\n\n{exception.Message}", "Deep Dive", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
