using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Desktop;
using Osynt.Desktop.Presentation;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunResponseInspectionChecks()
    {
        Check("Binary responses preserve exact bytes and content type for saving without a text preview", () =>
        {
            byte[] bytes = [0x89, 0x50, 0x4e, 0x47, 0, 0xff, 0xfe, 0x10];
            using var client = new HttpClient(new StubHandler(_ =>
            {
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }));
            var response = new RequestExecutor(client).SendAsync(new()
            {
                Url = "https://example.com"
            }, new Dictionary<string, string>()).Result;
            True(response.IsBinary);
            Equal("", response.Body);
            Equal("image/png", response.ContentType);
            True(ResponseExport.Bytes(response, "ignored").Span.SequenceEqual(bytes));
            Equal("response.png", ResponseExport.FileName(response));
            using var tab = new Osynt.Desktop.ViewModels.RequestTab(new()) { Response = response };
            True(tab.BinaryResponse && !tab.CanCopyResponse && !tab.ShowResponseBody && !tab.EmptyResponseBody);
        });
        Check("Binary previews obey the response limit and exported filenames identify incomplete files", () =>
        {
            var bytes = Enumerable.Repeat((byte)0xff, RequestExecutor.MaxResponseBytes + 1).ToArray();
            bytes[0] = 0;
            using var client = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })));
            var response = new RequestExecutor(client).SendAsync(new()
            {
                Url = "https://example.com"
            }, new Dictionary<string, string>()).Result;
            True(response.IsBinary && response.Truncated);
            Equal(RequestExecutor.MaxResponseBytes, response.BinaryBody!.Length);
            Equal("response-preview.bin", ResponseExport.FileName(response));
        });
        Check("BOMs, declared charsets, and structured text media types decode to usable text", () =>
        {
            foreach (var (encoding, type) in new (Encoding Encoding, string? Type)[]
            {
                (Encoding.UTF8, "application/problem+json"), (Encoding.Unicode, null),
                (Encoding.BigEndianUnicode, "text/plain"), (Encoding.UTF32, null),
                (Encoding.Latin1, "text/plain; charset=iso-8859-1")
            })
            {
                var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("café")).ToArray();
                using var client = new HttpClient(new StubHandler(_ =>
                {
                    var content = new ByteArrayContent(bytes);
                    if (type is not null)
                        content.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
                }));
                var response = new RequestExecutor(client).SendAsync(new()
                {
                    Url = "https://example.com"
                }, new Dictionary<string, string>()).Result;
                True(!response.IsBinary);
                Equal("café", response.Body);
                Equal(bytes.Length, (int)response.SizeBytes);
            }
        });
    }

    private static void RunResponseInspectionDesktopChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        using var workspace = CreateWorkspace(Path.Combine(directory, "response-inspection-ui"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace };
        window.Show();
        var tab = workspace.Requests.Selected;
        tab.Raw = true;
        Check("Response find supports case-insensitive matches, navigation, wrapping, and no-match feedback", () =>
        {
            const string body = "alpha\nBeta alpha\nALPHA";
            tab.Response = new(200, "OK", 1, body.Length, body, [new("Content-Type", "text/plain")]);
            Dispatcher.UIThread.RunJobs();
            Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Find"));
            var input = window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "ResponseSearchInput");
            True(input.IsEffectivelyVisible);
            input.Text = "alpha";
            Dispatcher.UIThread.RunJobs();
            var status = window.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "ResponseFindStatus");
            Equal("1 of 3", status.Text);
            var selection = window.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Text == body);
            Equal(0, selection.SelectionStart);
            Equal(5, selection.SelectionEnd);
            Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Next"));
            Equal("2 of 3", status.Text);
            Equal(11, selection.SelectionStart);
            Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Previous"));
            Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Previous"));
            Equal("3 of 3", status.Text);
            Equal(17, selection.SelectionStart);
            input.Text = "missing";
            Dispatcher.UIThread.RunJobs();
            Equal("No matches", status.Text);
            tab.Response = new(200, "OK", 1, 6, "aaa aa", []);
            input.Text = "aa";
            Dispatcher.UIThread.RunJobs();
            Equal("1 of 2", status.Text);
            Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Previous"));
            Equal("2 of 2", status.Text);
        });
        Check("Ctrl+F opens response search from headers and binary responses expose the proper actions", () =>
        {
            workspace.Editor.ResponsePaneCommand.Execute("Headers");
            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.F, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            True(workspace.Editor.ResponseBodyActive);
            True(window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "ResponseSearchInput").IsEffectivelyVisible);
            tab.Response = new(200, "OK", 1, 3, "", [new("Content-Type", "application/pdf")], BinaryBody: [1, 2, 3]);
            Dispatcher.UIThread.RunJobs();
            True(window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Binary response").IsEffectivelyVisible);
            True(!window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Copy").IsEnabled);
            True(window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Save" && b.IsEffectivelyVisible && b.Classes.Contains("icon")).IsEnabled);
            workspace.Editor.ResponsePaneCommand.Execute("Headers");
            Dispatcher.UIThread.RunJobs();
            True(window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Copy headers").IsEffectivelyVisible);
        });
        Check("Response search selects highlighted JSON and scrolls to distant matches", () =>
        {
            workspace.Editor.ResponsePaneCommand.Execute("Body");
            tab.Raw = false;
            tab.Response = new(200, "OK", 1, 20, "{\"name\":\"needle\"}", []);
            Dispatcher.UIThread.RunJobs();
            var viewer = window.GetVisualDescendants().OfType<Osynt.Desktop.Controls.CodeViewer>().Single();
            viewer.ShowFind();
            var input = viewer.GetVisualDescendants().OfType<TextBox>().Single();
            input.Text = "needle";
            Dispatcher.UIThread.RunJobs();
            var text = viewer.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
            Equal(tab.ResponseDisplay.IndexOf("needle", StringComparison.Ordinal), text.SelectionStart);
            Equal(text.SelectionStart + 6, text.SelectionEnd);
            tab.Raw = true;
            var body = string.Join('\n', Enumerable.Repeat("A line of response text", 200)) + "\nneedle";
            tab.Response = new(200, "OK", 1, body.Length, body, []);
            Dispatcher.UIThread.RunJobs();
            input.Text = "response";
            Dispatcher.UIThread.RunJobs();
            input.Text = "needle";
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            True(viewer.GetVisualDescendants().OfType<ScrollViewer>().Any(s => s.Offset.Y > 0));
        });
        window.Close();
    }
}
