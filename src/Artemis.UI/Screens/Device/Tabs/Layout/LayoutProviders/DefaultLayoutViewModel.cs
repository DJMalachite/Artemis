﻿using Artemis.Core;
using Artemis.Core.Providers;
using Artemis.UI.Shared;

namespace Artemis.UI.Screens.Device.Layout.LayoutProviders;

public class DefaultLayoutViewModel : ViewModelBase, ILayoutProviderViewModel
{
    private readonly DefaultLayoutProvider _layoutProvider;

    public DefaultLayoutViewModel(DefaultLayoutProvider layoutProvider)
    {
        _layoutProvider = layoutProvider;
    }

    /// <inheritdoc />
    public ILayoutProvider Provider => _layoutProvider;

    public ArtemisDevice Device { get; set; } = null!;

    /// <inheritdoc />
    public string Name => "Default";

    /// <inheritdoc />
    public string Description => "Attempts to load a layout from the default paths";

    /// <inheritdoc />
    public void Apply()
    {
        _layoutProvider.ConfigureDevice(Device);
    }
}