using System.Collections.ObjectModel;
using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

/// <summary>An editable presentation copy. Domain entities do not know about bindings or notifications.</summary>
public sealed class RequestEditorViewModel : ObservableModel
{
    private string _name;
    private string _method;
    private string _url;
    private string _body;
    public RequestEditorViewModel(RequestDocument model)
    {
        Id = model.Id;
        _name = model.Name;
        _method = model.Method;
        _url = model.Url;
        _body = model.Body;
        Parameters = new(model.Parameters.Select(RequestPairViewModel.FromModel));
        Headers = new(model.Headers.Select(RequestPairViewModel.FromModel));
    }
    public Guid Id
    {
        get;
    }
    public string Name
    {
        get => _name; set => Set(ref _name, value);
    }
    public string Method
    {
        get => _method; set => Set(ref _method, value);
    }
    public string Url
    {
        get => _url; set => Set(ref _url, value);
    }
    public string Body
    {
        get => _body; set => Set(ref _body, value);
    }
    public ObservableCollection<RequestPairViewModel> Parameters
    {
        get;
    }
    public ObservableCollection<RequestPairViewModel> Headers
    {
        get;
    }
    public RequestDocument Clone(bool newId = false) => new()
    {
        Id = newId ? Guid.NewGuid() : Id,
        Name = Name,
        Method = Method,
        Url = Url,
        Body = Body,
        Parameters = Parameters.Select(p => p.ToModel()).ToList(),
        Headers = Headers.Select(p => p.ToModel()).ToList()
    };
}
