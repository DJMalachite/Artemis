﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Artemis.Core;
using Artemis.Core.LayerBrushes;
using Artemis.Core.LayerEffects;
using Artemis.UI.Ninject.Factories;
using Artemis.UI.Screens.ProfileEditor.ProfileElementProperties.Timeline;
using Artemis.UI.Screens.ProfileEditor.ProfileElementProperties.Tree;
using Artemis.UI.Shared;
using Artemis.UI.Shared.Services.PropertyInput;
using ReactiveUI;

namespace Artemis.UI.Screens.ProfileEditor.ProfileElementProperties;

public class ProfileElementPropertyGroupViewModel : ViewModelBase
{
    private readonly ILayerPropertyVmFactory _layerPropertyVmFactory;
    private readonly IPropertyInputService _propertyInputService;
    private bool _hasChildren;
    private bool _isExpanded;
    private bool _isVisible;

    public ProfileElementPropertyGroupViewModel(LayerPropertyGroup layerPropertyGroup, ILayerPropertyVmFactory layerPropertyVmFactory, IPropertyInputService propertyInputService)
    {
        _layerPropertyVmFactory = layerPropertyVmFactory;
        _propertyInputService = propertyInputService;
        Children = new ObservableCollection<ViewModelBase>();
        LayerPropertyGroup = layerPropertyGroup;
        TreeGroupViewModel = layerPropertyVmFactory.TreeGroupViewModel(this);

        // TODO: Centralize visibility updating or do it here and dispose
        _isVisible = !LayerPropertyGroup.IsHidden;

        PopulateChildren();
    }

    public ProfileElementPropertyGroupViewModel(LayerPropertyGroup layerPropertyGroup, ILayerPropertyVmFactory layerPropertyVmFactory, IPropertyInputService propertyInputService,
        BaseLayerBrush layerBrush)
        : this(layerPropertyGroup, layerPropertyVmFactory, propertyInputService)
    {
        LayerBrush = layerBrush;
    }

    public ProfileElementPropertyGroupViewModel(LayerPropertyGroup layerPropertyGroup, ILayerPropertyVmFactory layerPropertyVmFactory, IPropertyInputService propertyInputService,
        BaseLayerEffect layerEffect)
        : this(layerPropertyGroup, layerPropertyVmFactory, propertyInputService)
    {
        LayerEffect = layerEffect;
    }

    public ObservableCollection<ViewModelBase> Children { get; }
    public LayerPropertyGroup LayerPropertyGroup { get; }
    public BaseLayerBrush? LayerBrush { get; }
    public BaseLayerEffect? LayerEffect { get; }

    public TreeGroupViewModel TreeGroupViewModel { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public bool HasChildren
    {
        get => _hasChildren;
        set => this.RaiseAndSetIfChanged(ref _hasChildren, value);
    }

    public List<ITimelineKeyframeViewModel> GetAllKeyframeViewModels(bool expandedOnly)
    {
        List<ITimelineKeyframeViewModel> result = new();
        if (expandedOnly && !IsExpanded)
            return result;

        foreach (ViewModelBase child in Children)
            if (child is ProfileElementPropertyViewModel profileElementPropertyViewModel)
                result.AddRange(profileElementPropertyViewModel.TimelinePropertyViewModel.GetAllKeyframeViewModels());
            else if (child is ProfileElementPropertyGroupViewModel profileElementPropertyGroupViewModel)
                result.AddRange(profileElementPropertyGroupViewModel.GetAllKeyframeViewModels(expandedOnly));

        return result;
    }

    private void PopulateChildren()
    {
        // Get all properties and property groups and create VMs for them
        // The group has methods for getting this without reflection but then we lose the order of the properties as they are defined on the group
        foreach (PropertyInfo propertyInfo in LayerPropertyGroup.GetType().GetProperties())
        {
            if (Attribute.IsDefined(propertyInfo, typeof(LayerPropertyIgnoreAttribute)))
                continue;

            if (typeof(ILayerProperty).IsAssignableFrom(propertyInfo.PropertyType))
            {
                ILayerProperty? value = (ILayerProperty?) propertyInfo.GetValue(LayerPropertyGroup);
                // Ensure a supported input VM was found, otherwise don't add it
                if (value != null && _propertyInputService.CanCreatePropertyInputViewModel(value))
                    Children.Add(_layerPropertyVmFactory.ProfileElementPropertyViewModel(value));
            }
            else if (typeof(LayerPropertyGroup).IsAssignableFrom(propertyInfo.PropertyType))
            {
                LayerPropertyGroup? value = (LayerPropertyGroup?) propertyInfo.GetValue(LayerPropertyGroup);
                if (value != null)
                    Children.Add(_layerPropertyVmFactory.ProfileElementPropertyGroupViewModel(value));
            }
        }

        HasChildren = Children.Any(i => i is ProfileElementPropertyViewModel {IsVisible: true} || i is ProfileElementPropertyGroupViewModel {IsVisible: true});
    }
}