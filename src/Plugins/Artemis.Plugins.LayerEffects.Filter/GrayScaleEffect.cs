﻿using Artemis.Core.Plugins.LayerEffect.Abstract;
using SkiaSharp;

namespace Artemis.Plugins.LayerEffects.Filter
{
    public class GrayScaleEffect : LayerEffect<GrayScaleEffectProperties>
    {
        public override void EnableLayerEffect()
        {
        }

        public override void DisableLayerEffect()
        {
        }

        public override void Update(double deltaTime)
        {
        }

        public override void PreProcess(SKCanvas canvas, SKImageInfo canvasInfo, SKPath path, SKPaint paint)
        {
            paint.ImageFilter = SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(new[]
            {
                0.21f, 0.72f, 0.07f, 0, 0,
                0.21f, 0.72f, 0.07f, 0, 0,
                0.21f, 0.72f, 0.07f, 0, 0,
                0, 0, 0, 1, 0
            }), paint.ImageFilter);
        }

        public override void PostProcess(SKCanvas canvas, SKImageInfo canvasInfo, SKPath path, SKPaint paint)
        {
        }
    }
}