﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.UI.DryIoc.Factories;
using Artemis.UI.Screens.Plugins.Prerequisites;
using Artemis.UI.Shared;
using Artemis.UI.Shared.Services;
using Avalonia.Threading;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using PropertyChanged.SourceGenerator;
using ReactiveUI;
using ContentDialogButton = Artemis.UI.Shared.Services.Builders.ContentDialogButton;

namespace Artemis.UI.Screens.Plugins;

public partial class PluginPrerequisitesInstallDialogViewModel : ContentDialogViewModelBase
{
    private CancellationTokenSource? _tokenSource;
    [Notify] private PluginPrerequisiteViewModel? _activePrerequisite;
    [Notify] private bool _canInstall;
    [Notify] private bool _showFailed;
    [Notify] private bool _showInstall = true;
    [Notify] private bool _showIntro = true;
    [Notify] private bool _showProgress;

    public PluginPrerequisitesInstallDialogViewModel(List<IPrerequisitesSubject> subjects, IPrerequisitesVmFactory prerequisitesVmFactory)
    {
        Prerequisites = new ObservableCollection<PluginPrerequisiteViewModel>();
        foreach (PluginPrerequisite prerequisite in subjects.SelectMany(prerequisitesSubject => prerequisitesSubject.PlatformPrerequisites))
            Prerequisites.Add(prerequisitesVmFactory.PluginPrerequisiteViewModel(prerequisite, false));
        Install = ReactiveCommand.CreateFromTask(ExecuteInstall, this.WhenAnyValue(vm => vm.CanInstall));

        Dispatcher.UIThread.Post(() => CanInstall = Prerequisites.Any(p => !p.PluginPrerequisite.IsMet()), DispatcherPriority.Background);
        this.WhenActivated(d =>
        {
            Disposable.Create(() =>
            {
                _tokenSource?.Cancel();
                _tokenSource?.Dispose();
                _tokenSource = null;
            }).DisposeWith(d);
        });
    }

    public ReactiveCommand<Unit, Unit> Install { get; }
    public ObservableCollection<PluginPrerequisiteViewModel> Prerequisites { get; }
    
    public static async Task Show(IWindowService windowService, List<IPrerequisitesSubject> subjects)
    {
        await windowService.CreateContentDialog()
            .WithTitle("Plugin prerequisites")
            .WithViewModel(out PluginPrerequisitesInstallDialogViewModel vm, subjects)
            .WithCloseButtonText("Cancel")
            .HavingPrimaryButton(b => b.WithText("Install").WithCommand(vm.Install))
            .WithDefaultButton(ContentDialogButton.Primary)
            .ShowAsync();
    }

    private async Task ExecuteInstall()
    {
        Deferral? deferral = null;
        if (ContentDialog != null)
            ContentDialog.Closing += (_, args) => deferral = args.GetDeferral();

        CanInstall = false;
        ShowFailed = false;
        ShowIntro = false;
        ShowProgress = true;

        _tokenSource?.Dispose();
        _tokenSource = new CancellationTokenSource();

        try
        {
            foreach (PluginPrerequisiteViewModel pluginPrerequisiteViewModel in Prerequisites)
            {
                pluginPrerequisiteViewModel.IsMet = pluginPrerequisiteViewModel.PluginPrerequisite.IsMet();
                if (pluginPrerequisiteViewModel.IsMet)
                    continue;

                ActivePrerequisite = pluginPrerequisiteViewModel;
                await ActivePrerequisite.Install(_tokenSource.Token);

                if (!ActivePrerequisite.IsMet)
                {
                    CanInstall = true;
                    ShowFailed = true;
                    ShowProgress = false;
                    return;
                }

                // Wait after the task finished for the user to process what happened
                if (pluginPrerequisiteViewModel != Prerequisites.Last())
                    await Task.Delay(250);
                else
                    await Task.Delay(1000);
            }

            if (deferral != null)
                deferral.Complete();
            else
                ContentDialog?.Hide(ContentDialogResult.Primary);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }
}