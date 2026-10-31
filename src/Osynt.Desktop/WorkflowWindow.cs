using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Osynt.Application.Workflows;
using Osynt.Desktop.ViewModels;
using Osynt.Domain.Requests;
using Osynt.Domain.Workflows;

namespace Osynt.Desktop;

public sealed class WorkflowWindow : Window
{
    private readonly WorkspaceViewModel workspace;
    private readonly ComboBox workflows = new() { MinWidth = 240 };
    private readonly TextBox name = new() { Watermark = "Workflow name" };
    private readonly StackPanel steps = new() { Spacing = 12 };
    private readonly StackPanel editing = new() { Spacing = 14 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel results = new() { Spacing = 6 };
    private readonly Dictionary<WorkflowStep, ResponseData> responses = new();
    private readonly List<Workflow> drafts;
    private Workflow? selected;
    private CancellationTokenSource? cancellation;

    public WorkflowWindow(WorkspaceViewModel workspace)
    {
        this.workspace = workspace;
        drafts = workspace.Workflows.List().ToList();
        Title = "Workflows";
        Width = 820;
        Height = 720;
        MinWidth = 760;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#F9F7F1");
        var root = new Grid { Margin = new Thickness(24), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(new TextBlock { Text = "Workflows", FontSize = 24, Margin = new Thickness(0, 0, 0, 18) });
        var scroll = new ScrollViewer { Content = editing };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(workflows);
        toolbar.Children.Add(Button("New", () => { var draft = new Workflow(); drafts.Add(draft); RefreshChoices(draft); }));
        toolbar.Children.Add(Button("Save", Save));
        toolbar.Children.Add(Button("Delete", () =>
        {
            if (selected is null)
                return;
            try
            {
                workspace.Workflows.Delete(selected.Id);
                drafts.Remove(selected);
                RefreshChoices(drafts.FirstOrDefault());
                status.Text = "Workflow deleted.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = ex.Message; }
        }));
        editing.Children.Add(toolbar);
        editing.Children.Add(name);
        editing.Children.Add(new TextBlock
        {
            Text = "Add requests in order, then select fields from their JSON responses to capture as environment variables. Use {{variable}} in later requests. Steps keep a copy of the request as added.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush.Parse("#68735F")
        });
        var add = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var choices = workspace.Library.Collections.SelectMany(c => c.Requests).Select(r => r.Document).ToList();
        var requestPicker = new ComboBox { Width = 260, ItemsSource = choices.Select(r => r.Method + " · " + r.Name).ToArray() };
        if (choices.Count > 0)
            requestPicker.SelectedIndex = 0;
        add.Children.Add(requestPicker);
        add.Children.Add(Button("Add saved request", () => { if (requestPicker.SelectedIndex >= 0) Add(choices[requestPicker.SelectedIndex]); }));
        add.Children.Add(Button("Add current tab", () => Add(workspace.Requests.Selected.Document.Clone())));
        editing.Children.Add(add);
        editing.Children.Add(steps);
        var bottom = new StackPanel { Spacing = 10, Margin = new Thickness(0, 14, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var run = new Button { Content = "Run workflow", Classes = { "primary" } };
        run.Click += async (_, _) => await Run();
        actions.Children.Add(run);
        actions.Children.Add(Button("Cancel run", () => cancellation?.Cancel()));
        bottom.Children.Add(actions);
        bottom.Children.Add(status);
        bottom.Children.Add(new ScrollViewer { Content = results, MaxHeight = 130 });
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);
        Content = root;
        workflows.SelectionChanged += (_, _) =>
        {
            selected = workflows.SelectedIndex >= 0 ? drafts[workflows.SelectedIndex] : null;
            name.Text = selected?.Name ?? "";
            RenderSteps();
        };
        name.TextChanged += (_, _) => { if (selected is not null) selected.Name = name.Text ?? ""; };
        Closing += (_, e) => { if (cancellation is not null) { e.Cancel = true; cancellation.Cancel(); status.Text = "Cancelling workflow…"; } };
        if (drafts.Count == 0)
            drafts.Add(new Workflow());
        RefreshChoices(drafts[0]);
    }

    private static Button Button(string label, Action action)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => action();
        return button;
    }
    private void RefreshChoices(Workflow? selection)
    {
        workflows.ItemsSource = drafts.Select(w => w.Name).ToArray();
        workflows.SelectedIndex = selection is null ? -1 : drafts.IndexOf(selection);
    }
    private void Add(RequestDocument request)
    {
        if (selected is null)
        {
            selected = new Workflow();
            drafts.Add(selected);
            RefreshChoices(selected);
        }
        selected.Steps.Add(new WorkflowStep { Request = request.Clone() });
        RenderSteps();
    }
    private void Save()
    {
        if (selected is null)
            return;
        try
        {
            workspace.Workflows.Save(selected);
            RefreshChoices(selected);
            status.Text = "Workflow saved.";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { status.Text = ex.Message; }
    }
    private void RenderSteps()
    {
        steps.Children.Clear();
        if (selected is null || selected.Steps.Count == 0)
        {
            steps.Children.Add(new TextBlock { Text = "Add your first request to start a workflow.", Margin = new Thickness(8) });
            return;
        }
        foreach (var step in selected.Steps)
        {
            var index = selected.Steps.IndexOf(step);
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = $"{index + 1}. {step.Request.Method} · {step.Request.Name}", FontWeight = FontWeight.SemiBold });
            panel.Children.Add(new TextBlock { Text = step.Request.Url, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#68735F") });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var capture = new Button { Content = "Select JSON fields…" };
            capture.Click += async (_, _) => await PickField(step);
            actions.Children.Add(capture);
            actions.Children.Add(Button("↑", () => Move(step, -1)));
            actions.Children.Add(Button("↓", () => Move(step, 1)));
            actions.Children.Add(Button("Remove", () => { selected.Steps.Remove(step); RenderSteps(); }));
            panel.Children.Add(actions);
            foreach (var mapping in step.Captures)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                row.Children.Add(new TextBlock { Text = $"{mapping.Pointer} → {{{{{mapping.Variable}}}}}", VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(Button("Remove capture", () => { step.Captures.Remove(mapping); RenderSteps(); }));
                panel.Children.Add(row);
            }
            steps.Children.Add(new Border { Child = panel, Background = Brush.Parse("#FFFDF8"), BorderBrush = Brush.Parse("#DCE1CF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14) });
        }
    }
    private void Move(WorkflowStep step, int offset)
    {
        if (selected is null)
            return;
        var index = selected.Steps.IndexOf(step);
        var target = index + offset;
        if (target < 0 || target >= selected.Steps.Count)
            return;
        selected.Steps.RemoveAt(index);
        selected.Steps.Insert(target, step);
        RenderSteps();
    }
    private async Task PickField(WorkflowStep step)
    {
        var response = responses.GetValueOrDefault(step) ?? workspace.Requests.Tabs
            .FirstOrDefault(t => t.Document.Id == step.Request.Id)?.Response;
        if (response is null)
        {
            status.Text = "Send this request in its tab first, or run the workflow, then select fields from its JSON response.";
            return;
        }
        IReadOnlyList<string> fields;
        try
        {
            fields = ResponseFields.List(response);
        }
        catch (ArgumentException ex) { status.Text = ex.Message; return; }
        var dialog = new Window { Title = "Capture a JSON response field", Width = 520, Height = 480, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new Grid { Margin = new Thickness(22), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto") };
        panel.Children.Add(new TextBlock { Text = "Select a response field", FontSize = 20, Margin = new Thickness(0, 0, 0, 12) });
        using var json = System.Text.Json.JsonDocument.Parse(response.Body);
        var list = new ListBox { ItemsSource = fields, SelectedIndex = fields.Count > 0 ? 0 : -1 };
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((pointer, _) =>
        {
            var value = json.RootElement;
            foreach (var segment in pointer!.Split('/').Skip(1))
            {
                var key = segment.Replace("~1", "/").Replace("~0", "~");
                value = value.ValueKind == System.Text.Json.JsonValueKind.Array ? value[int.Parse(key)] : value.GetProperty(key);
            }
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock { Text = pointer.Length == 0 ? "(root)" : pointer, FontSize = 11, Foreground = Brush.Parse("#68735F") });
            var raw = value.GetRawText();
            row.Children.Add(new TextBlock
            {
                Text = raw.Length > 120 ? raw[..120] + "…" : raw,
                FontFamily = new FontFamily("Cascadia Code, DejaVu Sans Mono, Consolas, monospace"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            return row;
        });
        Grid.SetRow(list, 1);
        panel.Children.Add(list);
        var variable = new TextBox { Watermark = "Environment variable name, e.g. access_token", Margin = new Thickness(0, 12, 0, 8) };
        Grid.SetRow(variable, 2);
        panel.Children.Add(variable);
        var note = new TextBlock { Text = "Future runs save this field before the next request. Existing variables with this name are replaced.", TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(note, 3);
        panel.Children.Add(note);
        var save = Button("Capture field", () =>
        {
            if (list.SelectedItem is not string pointer)
                return;
            var mapping = new ResponseCapture(pointer, variable.Text?.Trim() ?? "");
            try
            {
                ResponseFields.Extract(response, [mapping]);
                if (step.Captures.Any(c => c.Variable == mapping.Variable))
                    throw new ArgumentException("That variable already has a capture in this step.");
                step.Captures.Add(mapping);
                RenderSteps();
                dialog.Close();
                status.Text = "Capture added. Save the workflow to keep it.";
            }
            catch (ArgumentException ex) { note.Text = ex.Message; }
        });
        save.Margin = new Thickness(0, 12, 0, 0);
        Grid.SetRow(save, 4);
        panel.Children.Add(save);
        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }
    private async Task Run()
    {
        if (selected is null || cancellation is not null)
            return;
        cancellation = new CancellationTokenSource();
        editing.IsEnabled = false;
        results.Children.Clear();
        status.Text = "Running workflow…";
        try
        {
            workspace.Workflows.Save(selected);
            await workspace.Workflows.RunAsync(selected, (index, result) =>
            {
                if (result.Response is { } response)
                    responses[selected.Steps[index]] = response;
                results.Children.Add(new TextBlock { Text = $"{index + 1}. {result.Request.Name} · {result.Response?.StatusCode.ToString() ?? "Failed"}" });
            }, cancellation.Token);
            status.Text = "Workflow complete. Captured variables are saved in the environment.";
        }
        catch (OperationCanceledException) { status.Text = "Workflow cancelled. Completed captures remain in the environment."; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException) { status.Text = ex.Message; }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            editing.IsEnabled = true;
            workspace.Library.RefreshHistory();
            workspace.Garden.Refresh();
            workspace.Environment.Refresh();
        }
    }
}
