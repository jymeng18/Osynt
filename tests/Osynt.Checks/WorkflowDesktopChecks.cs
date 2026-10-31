using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Checks.Fakes;
using Osynt.Desktop;
using Osynt.Desktop.Composition;
using Osynt.Desktop.ViewModels;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void PressWorkflow(Window owner, string name) => owner.FindControl<Button>(name)!
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void RunWorkflowDesktopChecks(Window owner, string[] args)
    {
        Check("Workflow autosaves response capture and fills the next request without copying tokens", () =>
        {
            var store = new InMemoryWorkspaceStore();
            using var workspace = WorkspaceComposition.Create(store, new JsonCollectionSerializer(),
                new StubRequestExecutor((request, variables, _) =>
                {
                    if (request.Authentication.Mode == "Bearer")
                        Equal("desktop-token", Osynt.Application.Requests.TemplateResolver.Resolve(request.Authentication.Token, variables));
                    return Task.FromResult(new ResponseData(200, "OK", 1, 0, "{\"access_token\":\"desktop-token\",\"user\":{\"id\":42}}", []));
                }), "");
            workspace.Requests.Selected.Document.Url = "https://example.test/register";
            var window = new WorkflowWindow(workspace);
            _ = window.ShowDialog(owner);
            Dispatcher.UIThread.RunJobs();
            window.FindControl<TextBox>("WorkflowName")!.Text = "Authentication chain";
            PressWorkflow(window, "AddCurrent");
            PressWorkflow(window, "SendStep");
            Dispatcher.UIThread.RunJobs();
            var fields = window.FindControl<ListBox>("ResponseFields")!;
            True(fields.ItemCount == 2);
            fields.SelectedIndex = 0;
            Equal("access_token", window.FindControl<TextBox>("CaptureVariable")!.Text);
            PressWorkflow(window, "SetVariable");
            Equal("desktop-token", workspace.Environment.Variables["access_token"]);
            Equal("access_token", store.State!.Workflows.Single().Steps.Single().Captures.Single().Variable);
            workspace.Requests.Selected.Document.Headers.Add(new() { Key = "Authorization", Value = "Bearer old-token" });
            PressWorkflow(window, "AddCurrent");
            window.FindControl<ListBox>("StepList")!.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            var use = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Use as Bearer in next step");
            use.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Equal("{{access_token}}", store.State.Workflows.Single().Steps[1].Request.Authentication.Token);
            True(!store.State.Workflows.Single().Steps[1].Request.Headers.Any(h => h.Key == "Authorization"));
            window.FindControl<ListBox>("StepList")!.SelectedIndex = 1;
            var tokenInput = window.FindControl<TextBox>("StepToken")!;
            tokenInput.Focus();
            tokenInput.Text = "{{acc";
            tokenInput.CaretIndex = 5;
            Dispatcher.UIThread.RunJobs();
            var completion = window.FindControl<Osynt.Desktop.Controls.VariableCompletionView>("CompletionView")!;
            var suggestion = completion.FindControl<ListBox>("VariableSuggestions")!.Items.Cast<Osynt.Desktop.Controls.VariableSuggestion>().Single();
            Equal("access_token", suggestion.Name);
            Equal("Step 1", suggestion.Source);
            True(!suggestion.Preview.Contains("desktop-token"));
            window.KeyPressQwerty(Avalonia.Input.PhysicalKey.Tab, Avalonia.Input.RawInputModifiers.None);
            window.KeyReleaseQwerty(Avalonia.Input.PhysicalKey.Tab, Avalonia.Input.RawInputModifiers.None);
            Equal("{{access_token}}", tokenInput.Text);
            PressWorkflow(window, "RunWorkflow");
            True(window.FindControl<TextBlock>("RunStatus")!.Text!.Contains("complete"));
            if (args.Contains("--snapshot"))
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var bitmap = window.CaptureRenderedFrame()!;
                bitmap.Save("/tmp/osynt-workflow-redesign.png");
            }
            window.Close();
            var reopened = new WorkflowWindow(workspace);
            _ = reopened.ShowDialog(owner);
            Equal("Authentication chain", reopened.FindControl<TextBox>("WorkflowName")!.Text);
            Equal(2, reopened.FindControl<ListBox>("StepList")!.ItemCount);
            reopened.Close();
        });
        Check("Environment edits autosave on close and a failed save retains the editor", () =>
        {
            var store = new InMemoryWorkspaceStore();
            using var workspace = WorkspaceComposition.Create(store, new JsonCollectionSerializer(), new StubRequestExecutor((_, _, _) => Task.FromResult(SuccessResponse())), "");
            var window = new EnvironmentWindow(workspace);
            _ = window.ShowDialog(owner);
            Dispatcher.UIThread.RunJobs();
            var pairs = window.FindControl<ItemsControl>("VariableList")!.Items.Cast<RequestPairViewModel>().ToList();
            var variable = pairs.First(p => p.Key == "season");
            variable.Value = "autumn";
            store.FailWrites = true;
            window.Close();
            True(window.IsVisible);
            True(workspace.Environment.Variables["season"] != "autumn");
            store.FailWrites = false;
            window.Close();
            Equal("autumn", workspace.Environment.Variables["season"]);
            Equal("autumn", store.State!.Variables["season"]);
        });
        Check("Workflow flushes unfinished drafts on close and retains changes after save failures", () =>
        {
            var store = new InMemoryWorkspaceStore();
            using var workspace = WorkspaceComposition.Create(store, new JsonCollectionSerializer(), new StubRequestExecutor((_, _, _) => Task.FromResult(SuccessResponse())), "");
            var window = new WorkflowWindow(workspace);
            _ = window.ShowDialog(owner);
            window.FindControl<TextBox>("WorkflowName")!.Text = "Pending draft";
            store.FailWrites = true;
            window.Close();
            True(window.IsVisible);
            True(window.FindControl<Button>("RetrySave")!.IsVisible);
            True(store.State!.Workflows.Single().Name != "Pending draft");
            store.FailWrites = false;
            PressWorkflow(window, "RetrySave");
            window.Close();
            Equal("Pending draft", store.State.Workflows.Single().Name);
            Equal(0, store.State.Workflows.Single().Steps.Count);
            var reopened = new WorkflowWindow(workspace);
            _ = reopened.ShowDialog(owner);
            Equal("Pending draft", reopened.FindControl<TextBox>("WorkflowName")!.Text);
            PressWorkflow(reopened, "AddRequest");
            True(store.State.Workflows.Single().Steps.Count > 1, "Adding a folder should add its requests together.");
            reopened.FindControl<TextBox>("StepUrl")!.Text = "https://example.test/updated";
            reopened.Close();
            Equal("https://example.test/updated", store.State.Workflows.Single().Steps.Last().Request.Url);
        });
    }
}
