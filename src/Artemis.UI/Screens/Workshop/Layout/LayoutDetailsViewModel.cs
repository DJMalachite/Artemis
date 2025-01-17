using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.Core.Services;
using Artemis.UI.Screens.Workshop.Entries.Details;
using Artemis.UI.Screens.Workshop.Layout.Dialogs;
using Artemis.UI.Screens.Workshop.Parameters;
using Artemis.UI.Shared.Routing;
using Artemis.UI.Shared.Services;
using Artemis.WebClient.Workshop;
using Artemis.WebClient.Workshop.Services;
using PropertyChanged.SourceGenerator;
using StrawberryShake;

namespace Artemis.UI.Screens.Workshop.Layout;

public partial class LayoutDetailsViewModel : RoutableScreen<WorkshopDetailParameters>
{
    private readonly IWorkshopClient _client;
    private readonly IDeviceService _deviceService;
    private readonly IWindowService _windowService;
    private readonly Func<IGetEntryById_Entry, EntryInfoViewModel> _getEntryInfoViewModel;
    private readonly Func<IGetEntryById_Entry, EntryReleasesViewModel> _getEntryReleasesViewModel;
    private readonly Func<IGetEntryById_Entry, EntryImagesViewModel> _getEntryImagesViewModel;
    [Notify] private IGetEntryById_Entry? _entry;
    [Notify] private EntryInfoViewModel? _entryInfoViewModel;
    [Notify] private EntryReleasesViewModel? _entryReleasesViewModel;
    [Notify] private EntryImagesViewModel? _entryImagesViewModel;

    public LayoutDetailsViewModel(IWorkshopClient client,
        IDeviceService deviceService,
        IWindowService windowService,
        Func<IGetEntryById_Entry, EntryInfoViewModel> getEntryInfoViewModel,
        Func<IGetEntryById_Entry, EntryReleasesViewModel> getEntryReleasesViewModel,
        Func<IGetEntryById_Entry, EntryImagesViewModel> getEntryImagesViewModel)
    {
        _client = client;
        _deviceService = deviceService;
        _windowService = windowService;
        _getEntryInfoViewModel = getEntryInfoViewModel;
        _getEntryReleasesViewModel = getEntryReleasesViewModel;
        _getEntryImagesViewModel = getEntryImagesViewModel;
    }

    public override async Task OnNavigating(WorkshopDetailParameters parameters, NavigationArguments args, CancellationToken cancellationToken)
    {
        await GetEntry(parameters.EntryId, cancellationToken);
    }

    private async Task GetEntry(long entryId, CancellationToken cancellationToken)
    {
        IOperationResult<IGetEntryByIdResult> result = await _client.GetEntryById.ExecuteAsync(entryId, cancellationToken);
        if (result.IsErrorResult())
            return;

        Entry = result.Data?.Entry;
        if (Entry == null)
        {
            EntryInfoViewModel = null;
            EntryReleasesViewModel = null;
        }
        else
        {
            EntryInfoViewModel = _getEntryInfoViewModel(Entry);
            EntryReleasesViewModel = _getEntryReleasesViewModel(Entry);
            EntryImagesViewModel = _getEntryImagesViewModel(Entry);

            EntryReleasesViewModel.OnInstallationFinished = OnInstallationFinished;
        }
    }

    private async Task OnInstallationFinished(InstalledEntry installedEntry)
    {
        // Find compatible devices
        ArtemisLayout layout = new(Path.Combine(installedEntry.GetReleaseDirectory().FullName, "layout.xml"));
        List<ArtemisDevice> devices = _deviceService.Devices.Where(d => d.RgbDevice.DeviceInfo.DeviceType == layout.RgbLayout.Type).ToList();

        // If any are found, offer to apply
        if (devices.Any())
        {
            await _windowService.CreateContentDialog()
                .WithTitle("Apply layout to devices")
                .WithViewModel(out DeviceSelectionDialogViewModel vm, devices, installedEntry)
                .WithCloseButtonText(null)
                .HavingPrimaryButton(b => b.WithText("Continue").WithCommand(vm.Apply))
                .ShowAsync();
        }
    }
}