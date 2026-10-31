using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Osynt.Desktop.Controls;
using Osynt.Desktop.ViewModels;
using Osynt.Domain.Requests;
using Osynt.Domain.Workflows;
using JsonFields = Osynt.Application.Workflows.ResponseFields;

namespace Osynt.Desktop;

public partial class WorkflowWindow : Window
{
    private readonly WorkspaceViewModel workspace = null!;
    private readonly List<Workflow> workflows = [];
    private readonly HashSet<Workflow> dirty = [];
    private readonly Dictionary<WorkflowStep, ResponseData> responses = new();
    private readonly HashSet<WorkflowStep> invalidated = [];
    private readonly Dictionary<WorkflowStep, string> outcomes = new();
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private Workflow? selected;
    private WorkflowStep? step;
    private CancellationTokenSource? cancellation;
    private bool loading;
    private List<FieldChoice> fields = [];
    private List<RequestChoice> requests = [];
    private sealed record RequestChoice(string Label, IReadOnlyList<RequestDocument> Requests)
    {
        public override string ToString() => Label;
    }
    private sealed record FieldChoice(string Pointer, string Value, string Name);

    public WorkflowWindow() => InitializeComponent();

    public WorkflowWindow(WorkspaceViewModel workspace) : this()
    {
        this.workspace = workspace;
        workflows = workspace.Workflows.List().ToList();
        DataContext = workspace;
        _ = new VariableCompletion(this, Variables, markedFieldsOnly: true);
        saveTimer.Tick += (_, _) => Flush();
        WorkflowName.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty || loading || selected is null)
                return;
            selected.Name = WorkflowName.Text ?? "";
            Changed(debounce: true);
        };
        WorkflowPicker.SelectionChanged += (_, _) =>
        {
            if (loading)
                return;
            Flush();
            selected = WorkflowPicker.SelectedIndex < 0 ? null : workflows[WorkflowPicker.SelectedIndex];
            step = selected?.Steps.FirstOrDefault();
            LoadWorkflow();
        };
        StepList.ItemTemplate = new FuncDataTemplate<WorkflowStep>((item, _) =>
        {
            if (item is null)
                return new TextBlock();
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
            var enabled = new CheckBox { IsChecked = item.Enabled, VerticalAlignment = VerticalAlignment.Top };
            Avalonia.Automation.AutomationProperties.SetName(enabled, "Include " + item.Request.Name);
            enabled.IsCheckedChanged += (_, _) =>
            {
                item.Enabled = enabled.IsChecked == true;
                Changed();
                SequenceHint.Text = $"{selected?.Steps.Count(s => s.Enabled)} steps included · select one to edit";
            };
            row.Children.Add(enabled);
            var text = new StackPanel { Spacing = 5 };
            text.Children.Add(new TextBlock { Text = $"{selected?.Steps.IndexOf(item) + 1}. {item.Request.Name}", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold, FontSize = 12 });
            text.Children.Add(new TextBlock { Text = item.Request.Method + "  " + (outcomes.GetValueOrDefault(item) ?? $"{item.Captures.Count} variables"), FontSize = 10, Foreground = Brush.Parse("#68735F") });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return row;
        });
        ResponseFields.ItemTemplate = new FuncDataTemplate<FieldChoice>((field, _) =>
        {
            var row = new StackPanel { Spacing = 4, Margin = new Thickness(3) };
            row.Children.Add(new TextBlock { Text = field?.Pointer == "" ? "(root)" : field?.Pointer, FontSize = 10, Foreground = Brush.Parse("#68735F") });
            row.Children.Add(new TextBlock { Text = field?.Value, FontFamily = new FontFamily("Cascadia Code, DejaVu Sans Mono, Consolas, monospace"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            return row;
        });
        StepList.SelectionChanged += (_, _) => { if (!loading) { step = StepList.SelectedItem as WorkflowStep; LoadStep(); } };
        NewWorkflow.Click += (_, _) =>
        {
            Flush();
            selected = new Workflow();
            workflows.Add(selected);
            step = null;
            Changed();
            LoadWorkflow();
        };
        DeleteWorkflow.Click += (_, _) =>
        {
            if (selected is null)
                return;
            try
            {
                workspace.Workflows.Delete(selected.Id);
                dirty.Remove(selected);
                workflows.Remove(selected);
                selected = workflows.FirstOrDefault();
                step = selected?.Steps.FirstOrDefault();
                LoadWorkflow();
            }
            catch (Exception ex) when (IsExpected(ex)) { ShowSaveError(ex.Message); }
        };
        RetrySave.Click += (_, _) => Flush();
        AddCurrent.Click += (_, _) => Add([workspace.Requests.Selected.Document.Clone()]);
        AddRequest.Click += (_, _) => { if (RequestPicker.SelectedItem is RequestChoice choice) Add(choice.Requests); };
        RemoveStep.Click += (_, _) =>
        {
            if (selected is null || step is null)
                return;
            var index = selected.Steps.IndexOf(step);
            selected.Steps.Remove(step);
            responses.Remove(step);
            outcomes.Remove(step);
            step = selected.Steps.ElementAtOrDefault(Math.Min(index, selected.Steps.Count - 1));
            Changed();
            RenderSequence();
            LoadStep();
        };
        MoveUp.Click += (_, _) => Move(-1);
        MoveDown.Click += (_, _) => Move(1);
        StepUrl.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Edit(s => s.Request.Url = StepUrl.Text ?? ""); };
        StepBody.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Edit(s => s.Request.Body = StepBody.Text ?? ""); };
        StepToken.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Edit(s => s.Request.Authentication.Token = StepToken.Text ?? ""); };
        StepUsername.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Edit(s => s.Request.Authentication.Username = StepUsername.Text ?? ""); };
        StepPassword.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Edit(s => s.Request.Authentication.Password = StepPassword.Text ?? ""); };
        StepMethod.SelectionChanged += (_, _) => Edit(s => s.Request.Method = StepMethod.SelectedItem as string ?? "GET");
        StepAuth.SelectionChanged += (_, _) =>
        {
            Edit(s => s.Request.Authentication.Mode = StepAuth.SelectedItem as string ?? "None");
            StepToken.IsVisible = StepAuth.SelectedItem as string == "Bearer";
            BasicCredentials.IsVisible = StepAuth.SelectedItem as string == "Basic";
        };
        FieldSearch.TextChanged += (_, _) => FilterFields();
        ResponseFields.SelectionChanged += (_, _) =>
        {
            if (ResponseFields.SelectedItem is not FieldChoice field)
            {
                CaptureEditor.IsVisible = false;
                return;
            }
            CaptureVariable.Text = step?.Captures.FirstOrDefault(c => c.Pointer == field.Pointer)?.Variable ?? field.Name;
            CaptureEditor.IsVisible = true;
        };
        SetVariable.Click += (_, _) => Capture();
        SendStep.Click += async (_, _) => await SendOne();
        RunWorkflow.Click += async (_, _) => await Run();
        CancelRun.Click += (_, _) => cancellation?.Cancel();
        Closing += (_, e) =>
        {
            if (cancellation is not null)
            {
                cancellation.Cancel();
                e.Cancel = true;
                RunStatus.Text = "Stopping…";
                return;
            }
            if (!Flush())
            {
                e.Cancel = true;
                RunStatus.Text = "Changes could not be saved. Retry save before closing.";
            }
        };
        Closed += (_, _) => saveTimer.Stop();
        RefreshRequests();
        if (workflows.Count == 0)
        {
            selected = new Workflow();
            workflows.Add(selected);
            Changed();
        }
        else
            selected = workflows[0];
        step = selected.Steps.FirstOrDefault();
        LoadWorkflow();
    }

    private static bool IsExpected(Exception ex) => ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException;
    private void RefreshRequests()
    {
        requests = [];
        foreach (var folder in workspace.Library.GetAllCollections())
        {
            requests.Add(new RequestChoice("Folder · " + folder.Name, folder.Requests));
            requests.AddRange(folder.Requests.Select(r => new RequestChoice($"{r.Method} · {r.Name}", [r])));
        }
        RequestPicker.ItemsSource = requests;
        RequestPicker.SelectedIndex = requests.Count > 0 ? 0 : -1;
    }
    private void LoadWorkflow()
    {
        loading = true;
        WorkflowPicker.ItemsSource = workflows.Select(w => string.IsNullOrWhiteSpace(w.Name) ? "Untitled workflow" : w.Name).ToArray();
        WorkflowPicker.SelectedIndex = selected is null ? -1 : workflows.IndexOf(selected);
        WorkflowName.Text = selected?.Name ?? "";
        loading = false;
        RenderSequence();
        LoadStep();
    }
    private void RenderSequence()
    {
        loading = true;
        StepList.ItemsSource = selected?.Steps.ToArray() ?? [];
        StepList.SelectedItem = step;
        loading = false;
        SequenceHint.Text = selected is { Steps.Count: > 0 } ? $"{selected.Steps.Count(s => s.Enabled)} steps included · select one to edit" : "Add a folder or request below to begin.";
        RunWorkflow.IsEnabled = cancellation is null && selected?.Steps.Any(s => s.Enabled) == true;
        MoveUp.IsEnabled = selected is not null && step is not null && selected.Steps.IndexOf(step) > 0;
        MoveDown.IsEnabled = selected is not null && step is not null && selected.Steps.IndexOf(step) < selected.Steps.Count - 1;
        RemoveStep.IsEnabled = step is not null;
    }
    private void LoadStep()
    {
        loading = true;
        EmptyInspector.IsVisible = step is null;
        StepInspector.IsVisible = step is not null;
        if (step is not null)
        {
            StepTitle.Text = step.Request.Name;
            StepMethod.SelectedItem = step.Request.Method;
            StepUrl.Text = step.Request.Url;
            StepAuth.SelectedItem = step.Request.Authentication.Mode;
            StepToken.Text = step.Request.Authentication.Token;
            StepBody.Text = step.Request.Body;
            StepUsername.Text = step.Request.Authentication.Username;
            StepPassword.Text = step.Request.Authentication.Password;
        }
        loading = false;
        RenderResponse();
        RenderCaptures();
        RenderSequence();
    }
    private void Add(IEnumerable<RequestDocument> documents)
    {
        if (selected is null)
        {
            selected = new Workflow();
            workflows.Add(selected);
        }
        foreach (var request in documents)
        {
            var added = new WorkflowStep { Request = request.Clone() };
            selected.Steps.Add(added);
            step = added;
        }
        Changed();
        LoadWorkflow();
    }
    private void Move(int offset)
    {
        if (selected is null || step is null)
            return;
        var index = selected.Steps.IndexOf(step);
        var target = index + offset;
        if (target < 0 || target >= selected.Steps.Count)
            return;
        selected.Steps.RemoveAt(index);
        selected.Steps.Insert(target, step);
        Changed();
        RenderSequence();
    }
    private void Edit(Action<WorkflowStep> edit)
    {
        if (loading || step is null)
            return;
        edit(step);
        responses.Remove(step);
        outcomes.Remove(step);
        invalidated.Add(step);
        RenderResponse();
        Changed(debounce: true);
    }
    private void Changed(bool debounce = false)
    {
        if (selected is null)
            return;
        dirty.Add(selected);
        SaveStatus.Text = "Saving…";
        RetrySave.IsVisible = false;
        saveTimer.Stop();
        if (debounce)
            saveTimer.Start();
        else
            Flush();
        RunWorkflow.IsEnabled = cancellation is null && selected.Steps.Any(s => s.Enabled);
    }
    private bool Flush()
    {
        saveTimer.Stop();
        var hadChanges = dirty.Count > 0;
        foreach (var workflow in dirty.ToArray())
        {
            try
            {
                workspace.Workflows.Save(workflow);
                dirty.Remove(workflow);
            }
            catch (Exception ex) when (IsExpected(ex)) { ShowSaveError(ex.Message); return false; }
        }
        if (hadChanges)
        {
            var index = WorkflowPicker.SelectedIndex;
            loading = true;
            WorkflowPicker.ItemsSource = workflows.Select(w => string.IsNullOrWhiteSpace(w.Name) ? "Untitled workflow" : w.Name).ToArray();
            WorkflowPicker.SelectedIndex = index;
            loading = false;
        }
        SaveStatus.Text = "All changes saved";
        SaveStatus.Foreground = Brush.Parse("#68735F");
        RetrySave.IsVisible = false;
        return true;
    }
    private void ShowSaveError(string message)
    {
        SaveStatus.Text = "Not saved";
        SaveStatus.Foreground = Brush.Parse("#A05D3B");
        RetrySave.IsVisible = true;
        RunStatus.Text = "Could not save: " + message;
    }
    private ResponseData? Response() => step is null ? null : responses.GetValueOrDefault(step) ?? (invalidated.Contains(step) ? null : workspace.Requests.Tabs
        .FirstOrDefault(t => t.Document.Id == step.Request.Id && t.Document.Url == step.Request.Url && t.Document.Body == step.Request.Body && t.Document.Method == step.Request.Method)?.Response);
    private void RenderResponse()
    {
        fields = [];
        CaptureEditor.IsVisible = false;
        FieldSearch.Text = "";
        var response = Response();
        ResponseStatus.Text = response is null ? "Not sent" : $"{response.StatusCode} · {response.ElapsedMilliseconds} ms";
        ResponseHint.Text = "Send this step to see its JSON fields, then select a value to reuse.";
        if (response is not null)
        {
            try
            {
                fields = JsonFields.Describe(response).Select(f => new FieldChoice(f.Pointer,
                    f.Value.Length > 150 ? f.Value[..150] + "…" : f.Value, f.SuggestedName)).ToList();
                ResponseHint.Text = fields.Count == 0 ? "This response has no values to capture." : "Select a JSON value. Its variable name is filled in for you.";
            }
            catch (ArgumentException ex) { ResponseHint.Text = ex.Message; }
        }
        FieldSearch.IsVisible = ResponseFields.IsVisible = fields.Count > 0;
        FilterFields();
    }
    private void FilterFields()
    {
        var query = FieldSearch.Text?.Trim() ?? "";
        ResponseFields.ItemsSource = fields.Where(f => f.Pointer.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        CaptureEditor.IsVisible = false;
    }
    private void Capture()
    {
        if (selected is null || step is null || ResponseFields.SelectedItem is not FieldChoice field || Response() is not { } response)
            return;
        if (!Flush())
            return;
        var mapping = new ResponseCapture(field.Pointer, CaptureVariable.Text?.Trim() ?? "");
        try
        {
            var existing = step.Captures.FirstOrDefault(c => c.Variable == mapping.Variable && c.Pointer != mapping.Pointer);
            if (existing is not null)
                throw new ArgumentException("This step already fills that variable from another field. Choose a different name.");
            workspace.Workflows.SaveCapture(selected, selected.Steps.IndexOf(step), response, mapping);
            workspace.Environment.Refresh();
            RenderCaptures();
            RenderSequence();
            RunStatus.Text = $"Saved {{{{{mapping.Variable}}}}}. It will refresh after this step runs.";
            SaveStatus.Text = "All changes saved";
        }
        catch (Exception ex) when (IsExpected(ex)) { RunStatus.Text = ex.Message; }
    }
    private void RenderCaptures()
    {
        CaptureList.Children.Clear();
        if (step is null || selected is null)
            return;
        foreach (var capture in step.Captures)
        {
            var row = new StackPanel { Spacing = 6 };
            row.Children.Add(new TextBlock { Text = $"{capture.Pointer} → {{{{{capture.Variable}}}}}", FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var remove = new Button { Content = "Remove variable link", FontSize = 11 };
            remove.Click += (_, _) => { step.Captures.Remove(capture); Changed(); RenderCaptures(); RenderSequence(); };
            actions.Children.Add(remove);
            var next = selected.Steps.Skip(selected.Steps.IndexOf(step) + 1).FirstOrDefault(s => s.Enabled);
            if (next is not null)
            {
                var use = new Button { Content = "Use as Bearer in next step", FontSize = 11 };
                use.Click += (_, _) =>
                {
                    next.Request.Headers.RemoveAll(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
                    next.Request.Authentication.Mode = "Bearer";
                    next.Request.Authentication.Token = "{{" + capture.Variable + "}}";
                    Changed();
                    RunStatus.Text = $"{next.Request.Name} now uses {{{{{capture.Variable}}}}} as its Bearer token.";
                };
                actions.Children.Add(use);
            }
            row.Children.Add(actions);
            CaptureList.Children.Add(new Border { Child = row, Background = Brush.Parse("#E8EEDC"), CornerRadius = new CornerRadius(5), Padding = new Thickness(10) });
        }
    }
    private IEnumerable<VariableSuggestion> Variables()
    {
        var values = workspace.Environment.Variables;
        // Only upstream steps can provide values for the selected request.
        if (selected is not null && step is not null)
            foreach (var upstream in selected.Steps.Take(selected.Steps.IndexOf(step)).Where(s => s.Enabled).Reverse())
                foreach (var capture in upstream.Captures)
                    yield return new VariableSuggestion(capture.Variable, "Step " + (selected.Steps.IndexOf(upstream) + 1),
                        values.TryGetValue(capture.Variable, out var value) ? VariableSuggestion.PreviewValue(capture.Variable, value) : "Filled when this step runs");
        foreach (var (name, value) in values)
            yield return VariableSuggestion.Environment(name, value);
    }
    private void BeginRun()
    {
        cancellation = new CancellationTokenSource();
        SequenceEditing.IsEnabled = false;
        StepInspector.IsEnabled = false;
        WorkflowName.IsEnabled = false;
        CancelRun.IsEnabled = true;
        RunWorkflow.IsEnabled = false;
    }
    private void EndRun()
    {
        cancellation?.Dispose();
        cancellation = null;
        SequenceEditing.IsEnabled = true;
        StepInspector.IsEnabled = true;
        WorkflowName.IsEnabled = true;
        CancelRun.IsEnabled = false;
        workspace.Library.RefreshHistory();
        workspace.Garden.Refresh();
        workspace.Environment.Refresh();
        RenderSequence();
        RenderResponse();
        RenderCaptures();
    }
    private async Task SendOne()
    {
        if (step is null || cancellation is not null || !Flush())
            return;
        var sending = step;
        BeginRun();
        RunStatus.Text = "Sending " + sending.Request.Name + "…";
        try
        {
            var result = await workspace.Workflows.SendStepAsync(sending, cancellation!.Token, received =>
            {
                if (received.Response is { } response)
                    responses[sending] = response;
            });
            if (result.Response is { } response)
                responses[sending] = response;
            outcomes[sending] = result.Response?.StatusCode.ToString() ?? "Failed";
            RunStatus.Text = result.Error ?? $"{sending.Request.Name} · {result.Response?.StatusCode}. Select a response value below to reuse it.";
        }
        catch (OperationCanceledException) { RunStatus.Text = "Step cancelled."; }
        catch (Exception ex) when (IsExpected(ex)) { RunStatus.Text = ex.Message; }
        finally { EndRun(); }
    }
    private async Task Run()
    {
        if (selected is null || cancellation is not null || !Flush())
            return;
        BeginRun();
        outcomes.Clear();
        var completed = 0;
        RunStatus.Text = "Running " + selected.Name + "…";
        try
        {
            await workspace.Workflows.RunAsync(selected, (index, result) =>
            {
                var current = selected.Steps[index];
                completed++;
                if (result.Response is { } response)
                    responses[current] = response;
                outcomes[current] = result.Response?.StatusCode.ToString() ?? "Failed";
                RunSummary.Text = $"{completed}/{selected.Steps.Count(s => s.Enabled)} · {current.Request.Name} · {outcomes[current]}";
            }, cancellation!.Token);
            RunStatus.Text = "Workflow complete · variables updated";
        }
        catch (OperationCanceledException) { RunStatus.Text = "Stopped · completed captures are saved"; }
        catch (Exception ex) when (IsExpected(ex)) { RunStatus.Text = ex.Message; }
        finally { EndRun(); }
    }
}
