using System.Collections.ObjectModel;
using System.Windows.Input;
using Plugin.Maui.Audio;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public class PlaceDetailViewModel : BaseViewModel
{
    private PlaceItem? _place;
    private NarrationLanguage? _selectedLanguage;
    private bool _isPlaying;
    private bool _isPaused;
    private double _audioProgress;
    private double _audioDuration;
    private double _audioPosition;
    private string _currentTime = "00:00";
    private string _totalTime = "00:00";
    private double _playbackSpeed = 1.0;
    private bool _isPlayerVisible;
    private bool _isFavorite;
    private string _message = string.Empty;
    private CancellationTokenSource? _positionCts;

    private IAudioPlayer? _audioPlayer;
    private readonly IAudioManager _audioManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserPoiStateService _stateService;
    private readonly IAuthService _authService;
    private readonly IApiService _apiService;
    private readonly IAchievementService _achievementService;
    private Stream? _audioStream;
    private bool _playbackLogged;
    private bool _isUpdatingProgress;
    private bool _isCheckingIn;
    private string _checkInText = "Check-in khám phá • +10 điểm";

    public ICommand PlayNarrationCommand { get; }
    public ICommand TogglePlaybackCommand { get; }
    public ICommand PauseNarrationCommand { get; }
    public ICommand StopNarrationCommand { get; }
    public ICommand SkipForwardCommand { get; }
    public ICommand SkipBackwardCommand { get; }
    public ICommand SelectLanguageCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand WatchVideoCommand { get; }
    public ICommand CheckInCommand { get; }

    public PlaceItem? Place
    {
        get => _place;
        set => SetProperty(ref _place, value);
    }

    public NarrationLanguage? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(CanWatchVideo));
                if (_isPlaying || _isPaused)
                    StopNarration();
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        set => SetProperty(ref _isPaused, value);
    }

    public double AudioProgress
    {
        get => _audioProgress;
        set
        {
            if (SetProperty(ref _audioProgress, value) &&
                !_isUpdatingProgress &&
                _audioPlayer != null &&
                _audioDuration > 0)
            {
                var seekPosition = value * _audioDuration;
                _audioPlayer.Seek(seekPosition);
            }
        }
    }

    public double AudioDuration
    {
        get => _audioDuration;
        set => SetProperty(ref _audioDuration, value);
    }

    public double AudioPosition
    {
        get => _audioPosition;
        set => SetProperty(ref _audioPosition, value);
    }

    public string CurrentTime
    {
        get => _currentTime;
        set => SetProperty(ref _currentTime, value);
    }

    public string TotalTime
    {
        get => _totalTime;
        set => SetProperty(ref _totalTime, value);
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (SetProperty(ref _playbackSpeed, value))
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.Speed = (float)value;
                }
            }
        }
    }

    public bool IsPlayerVisible
    {
        get => _isPlayerVisible;
        set => SetProperty(ref _isPlayerVisible, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteIcon));
                OnPropertyChanged(nameof(FavoriteText));
            }
        }
    }

    public string FavoriteIcon => IsFavorite ? "♥" : "♡";
    public string FavoriteText => IsFavorite ? "Đã yêu thích" : "Lưu yêu thích";

    public string Message
    {
        get => _message;
        set
        {
            SetProperty(ref _message, value);
            OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool CanPlay =>
        SelectedLanguage != null &&
        !string.IsNullOrWhiteSpace(SelectedLanguage.AudioUrl);

    public bool CanWatchVideo =>
        SelectedLanguage != null &&
        !string.IsNullOrWhiteSpace(SelectedLanguage.VideoUrl);

    public bool HasNarrationLanguages => NarrationLanguages.Count > 0;
    public bool HasNoNarrationLanguages => !HasNarrationLanguages;

    public bool IsCheckingIn
    {
        get => _isCheckingIn;
        set => SetProperty(ref _isCheckingIn, value);
    }

    public string CheckInText
    {
        get => _checkInText;
        set => SetProperty(ref _checkInText, value);
    }

    public List<NarrationLanguage> NarrationLanguages => Place?.NarrationLanguages ?? new();

    public ObservableCollection<double> SpeedOptions { get; } = new() { 0.5, 1.0, 1.5, 2.0 };

    public PlaceDetailViewModel(
        IAudioManager audioManager,
        IHttpClientFactory httpClientFactory,
        IUserPoiStateService stateService,
        IAuthService authService,
        IApiService apiService,
        IAchievementService achievementService)
    {
        _audioManager = audioManager;
        _httpClientFactory = httpClientFactory;
        _stateService = stateService;
        _authService = authService;
        _apiService = apiService;
        _achievementService = achievementService;
        PlayNarrationCommand = new Command(async () => await PlayNarrationAsync());
        TogglePlaybackCommand = new Command(async () =>
        {
            if (IsPlaying)
                PauseNarration();
            else
                await PlayNarrationAsync();
        });
        PauseNarrationCommand = new Command(PauseNarration);
        StopNarrationCommand = new Command(StopNarration);
        SkipForwardCommand = new Command(SkipForward);
        SkipBackwardCommand = new Command(SkipBackward);
        SelectLanguageCommand = new Command<NarrationLanguage>(OnLanguageSelected);
        ToggleFavoriteCommand = new Command(async () => await ToggleFavoriteAsync());
        WatchVideoCommand = new Command(async () => await WatchVideoAsync());
        CheckInCommand = new Command(async () => await CheckInAsync());
    }

    private void OnLanguageSelected(NarrationLanguage? language)
    {
        if (language != null)
        {
            SelectedLanguage = language;
            // If currently playing, stop and restart with new language
            if (_isPlaying || _isPaused)
            {
                StopNarration();
            }
        }
    }

    public async Task LoadPlaceAsync(PlaceItem place)
    {
        StopNarration();
        Place = place;
        Message = string.Empty;
        CheckInText = "Check-in khám phá • +10 điểm";
        _playbackLogged = false;
        OnPropertyChanged(nameof(NarrationLanguages));
        OnPropertyChanged(nameof(HasNarrationLanguages));
        OnPropertyChanged(nameof(HasNoNarrationLanguages));

        if (NarrationLanguages.Count > 0)
        {
            SelectedLanguage = NarrationLanguages[0];
        }

        try
        {
            await _stateService.AddRecentAsync(place.Id);
            IsFavorite = await _stateService.IsFavoriteAsync(place.Id);
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }

        try
        {
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
                return;

            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            if (location is not null)
            {
                Place.Distance = Math.Round(
                    Location.CalculateDistance(
                        location,
                        new Location(place.Latitude, place.Longitude),
                        DistanceUnits.Kilometers),
                    1);
                OnPropertyChanged(nameof(Place));
            }
        }
        catch (Exception exception) when (exception is FeatureNotSupportedException or PermissionException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not calculate distance: {exception.Message}");
        }
    }

    public async Task PlayNarrationAsync()
    {
        if (_audioPlayer == null)
        {
            await InitializeAudioAsync();
        }

        if (_audioPlayer != null)
        {
            if (_isPaused)
            {
                _audioPlayer.Play();
                IsPaused = false;
                IsPlaying = true;
                StartPositionTracking();
            }
            else
            {
                _audioPlayer.Play();
                IsPlaying = true;
                IsPaused = false;
                IsPlayerVisible = true;
                StartPositionTracking();
            }
        }
    }

    public void PauseNarration()
    {
        if (_audioPlayer != null && _isPlaying)
        {
            _audioPlayer.Pause();
            IsPaused = true;
            IsPlaying = false;
            _positionCts?.Cancel();
        }
    }

    public void StopNarration()
    {
        _audioPlayer?.Stop();
        _audioPlayer?.Dispose();
        _audioPlayer = null;
        _audioStream?.Dispose();
        _audioStream = null;
        IsPlaying = false;
        IsPaused = false;
        IsPlayerVisible = false;
        AudioProgress = 0;
        AudioPosition = 0;
        CurrentTime = "00:00";
        _positionCts?.Cancel();
    }

    public void SkipForward()
    {
        if (_audioPlayer != null && _audioDuration > 0)
        {
            var position = _audioPlayer.CurrentPosition;
            var newPosition = Math.Min(position + 10, _audioDuration);
            _audioPlayer.Seek(newPosition);
        }
    }

    public void SkipBackward()
    {
        if (_audioPlayer != null)
        {
            var position = _audioPlayer.CurrentPosition;
            var newPosition = Math.Max(position - 10, 0);
            _audioPlayer.Seek(newPosition);
        }
    }

    public void SetSpeed(double speed)
    {
        PlaybackSpeed = speed;
    }

    private async Task InitializeAudioAsync()
    {
        try
        {
            var audioUrl = SelectedLanguage?.AudioUrl;
            if (string.IsNullOrWhiteSpace(audioUrl))
                return;

            _audioStream = Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri)
                ? await _httpClientFactory.CreateClient().GetStreamAsync(uri)
                : await FileSystem.OpenAppPackageFileAsync(audioUrl);
            _audioPlayer = _audioManager.CreatePlayer(_audioStream);
            _audioPlayer.PlaybackEnded += OnPlaybackEnded;

            AudioDuration = _audioPlayer.Duration;
            TotalTime = FormatTime(AudioDuration);
            _audioPlayer.Speed = (float)PlaybackSpeed;
            await LogPlaybackAsync();
        }
        catch (Exception ex)
        {
            Message = "Không thể phát file thuyết minh. Vui lòng kiểm tra kết nối và thử lại.";
            System.Diagnostics.Debug.WriteLine($"Error initializing audio: {ex.Message}");
        }
    }

    private void OnPlaybackEnded(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = false;
            IsPaused = false;
            AudioProgress = 1.0;
            AudioPosition = AudioDuration;
            CurrentTime = TotalTime;
            _positionCts?.Cancel();
        });
    }

    private void StartPositionTracking()
    {
        _positionCts?.Cancel();
        _positionCts = new CancellationTokenSource();
        var token = _positionCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && _audioPlayer != null)
            {
                if (_audioPlayer.IsPlaying)
                {
                    var position = _audioPlayer.CurrentPosition;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        AudioPosition = position;
                        CurrentTime = FormatTime(position);
                        if (AudioDuration > 0)
                        {
                            _isUpdatingProgress = true;
                            AudioProgress = position / AudioDuration;
                            _isUpdatingProgress = false;
                        }
                    });
                }
                await Task.Delay(250, token);
            }
        }, token);
    }

    private static string FormatTime(double totalSeconds)
    {
        var time = TimeSpan.FromSeconds(totalSeconds);
        return time.TotalHours >= 1
            ? $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private async Task ToggleFavoriteAsync()
    {
        if (Place is null)
            return;

        try
        {
            Message = string.Empty;
            IsFavorite = await _stateService.ToggleFavoriteAsync(Place.Id);
            Message = IsFavorite
                ? "Đã thêm vào địa điểm yêu thích."
                : "Đã xóa khỏi địa điểm yêu thích.";
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
    }

    private async Task LogPlaybackAsync()
    {
        if (_playbackLogged || Place is null || !int.TryParse(Place.Id, out var poiId))
            return;

        _playbackLogged = true;
        var profile = await _authService.GetProfileAsync();
        var deviceId = Preferences.Get("device_id", string.Empty);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = Guid.NewGuid().ToString("N");
            Preferences.Set("device_id", deviceId);
        }

        await _apiService.PostAsync<object>("api/log", new
        {
            PoiId = poiId,
            TouristId = profile?.Id,
            DeviceId = deviceId,
            LanguageCode = SelectedLanguage?.Code ?? "vi",
            ListenDuration = 0,
            CreatedAt = DateTime.Now
        });
    }

    private async Task WatchVideoAsync()
    {
        var videoUrl = SelectedLanguage?.VideoUrl;
        if (string.IsNullOrWhiteSpace(videoUrl))
            return;

        try
        {
            await Browser.Default.OpenAsync(videoUrl, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception exception)
        {
            Message = "Không thể mở video thuyết minh.";
            System.Diagnostics.Debug.WriteLine($"Could not open video: {exception.Message}");
        }
    }

    private async Task CheckInAsync()
    {
        if (Place is null || IsCheckingIn)
            return;

        try
        {
            IsCheckingIn = true;
            Message = string.Empty;
            var result = await _achievementService.CheckInByGpsAsync(Place.Id);
            CheckInText = result.IsNewDiscovery
                ? $"Đã khám phá • +{result.PointsAwarded} điểm"
                : "Đã khám phá POI này";
            Message = result.BuildDisplayMessage();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or FeatureNotSupportedException or PermissionException)
        {
            Message = exception.Message;
        }
        finally
        {
            IsCheckingIn = false;
        }
    }
}
