using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Osynt.Application.Requests;
using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

/// <summary>An editable presentation copy. Domain entities do not know about bindings or notifications.</summary>
public sealed class RequestEditorViewModel : ObservableModel, IDisposable
{
    private string _name;
    private string _method;
    private string _url;
    private string _body;
    private string _bodyMode;
    private int _timeoutSeconds;
    private string _authMode;
    private string _bearerToken;
    private string _authUsername;
    private string _authPassword;
    private bool _synchronizing;
    private readonly HashSet<RequestPairViewModel> _observedParameters = [];
    public RequestEditorViewModel(RequestDocument model)
    {
        Id = model.Id;
        _name = model.Name;
        _method = model.Method;
        _body = model.Body;
        _bodyMode = model.BodyMode;
        _timeoutSeconds = model.TimeoutSeconds;
        _authMode = model.Authentication.Mode;
        _bearerToken = model.Authentication.Token;
        _authUsername = model.Authentication.Username;
        _authPassword = model.Authentication.Password;
        var rowKeys = model.Parameters.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        Parameters = new(RequestUrl.ParseParameters(model.Url).Where(p => !rowKeys.Contains(p.Key))
            .Concat(model.Parameters).Select(RequestPairViewModel.FromModel));
        _url = RequestUrl.WithParameters(model.Url, Parameters.Select(p => p.ToModel()));
        Headers = new(model.Headers.Select(RequestPairViewModel.FromModel));
        FormFields = new(model.FormFields.Select(RequestPairViewModel.FromModel));
        Parameters.CollectionChanged += ParametersChanged;
        ObserveParameters();
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
        get => _url;
        set
        {
            var previousAddress = RequestUrl.Address(_url);
            if (!Set(ref _url, value) || _synchronizing)
                return;
            _synchronizing = true;
            try
            {
                var disabled = previousAddress == RequestUrl.Address(value)
                    ? Parameters.Where(p => !p.Enabled).ToArray() : [];
                Parameters.Clear();
                foreach (var pair in RequestUrl.ParseParameters(value))
                    Parameters.Add(RequestPairViewModel.FromModel(pair));
                foreach (var pair in disabled)
                    Parameters.Add(pair);
            }
            finally { _synchronizing = false; }
        }
    }
    public string Body
    {
        get => _body; set => Set(ref _body, value);
    }
    public int TimeoutSeconds
    {
        get => _timeoutSeconds; set => Set(ref _timeoutSeconds, value);
    }
    public string BodyMode
    {
        get => _bodyMode;
        set
        {
            if (!Set(ref _bodyMode, value))
                return;
            Raise(nameof(JsonBody));
            Raise(nameof(TextBody));
            Raise(nameof(FormBody));
            Raise(nameof(NoBody));
            Raise(nameof(BodyWatermark));
        }
    }
    public bool JsonBody => BodyMode == RequestBodyModes.Json;
    public bool TextBody => BodyMode is RequestBodyModes.Json or RequestBodyModes.Text;
    public bool FormBody => BodyMode == RequestBodyModes.Form;
    public bool NoBody => BodyMode == RequestBodyModes.None;
    public string AuthMode
    {
        get => _authMode;
        set
        {
            if (!Set(ref _authMode, value))
                return;
            Raise(nameof(BearerAuth));
            Raise(nameof(BasicAuth));
            Raise(nameof(NoAuth));
        }
    }
    public bool BearerAuth => AuthMode == RequestAuthentication.Bearer;
    public bool BasicAuth => AuthMode == RequestAuthentication.Basic;
    public bool NoAuth => AuthMode == RequestAuthentication.None;
    public string BearerToken
    {
        get => _bearerToken; set => Set(ref _bearerToken, value);
    }
    public string AuthUsername
    {
        get => _authUsername; set => Set(ref _authUsername, value);
    }
    public string AuthPassword
    {
        get => _authPassword; set => Set(ref _authPassword, value);
    }
    public string BodyWatermark => JsonBody ? "Enter JSON…" : "Enter plain text…";
    public ObservableCollection<RequestPairViewModel> FormFields
    {
        get;
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
        BodyMode = BodyMode,
        FormFields = FormFields.Select(p => p.ToModel()).ToList(),
        Authentication = new()
        {
            Mode = AuthMode,
            Token = BearerToken,
            Username = AuthUsername,
            Password = AuthPassword
        },
        TimeoutSeconds = TimeoutSeconds,
        Parameters = Parameters.Select(p => p.ToModel()).ToList(),
        Headers = Headers.Select(p => p.ToModel()).ToList()
    };

    private void ParametersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ObserveParameters();
        SynchronizeUrl();
    }
    private void ParameterChanged(object? sender, PropertyChangedEventArgs e) => SynchronizeUrl();
    private void SynchronizeUrl()
    {
        if (_synchronizing)
            return;
        _synchronizing = true;
        try
        {
            Set(ref _url, RequestUrl.WithParameters(_url, Parameters.Select(p => p.ToModel())), nameof(Url));
        }
        finally { _synchronizing = false; }
    }
    private void ObserveParameters()
    {
        var current = Parameters.ToHashSet();
        foreach (var removed in _observedParameters.Except(current).ToArray())
        {
            removed.PropertyChanged -= ParameterChanged;
            _observedParameters.Remove(removed);
        }
        foreach (var added in current.Except(_observedParameters).ToArray())
        {
            added.PropertyChanged += ParameterChanged;
            _observedParameters.Add(added);
        }
    }
    public void Dispose()
    {
        Parameters.CollectionChanged -= ParametersChanged;
        foreach (var pair in _observedParameters)
            pair.PropertyChanged -= ParameterChanged;
    }
}
