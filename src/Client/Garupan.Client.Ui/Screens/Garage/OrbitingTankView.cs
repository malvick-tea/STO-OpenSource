using System;
using System.Numerics;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Garage;

/// <summary>
/// Owns the 3D tank pedestal: model load, slow Y-axis orbit, and per-frame draw via
/// <see cref="IModelRenderer"/>. Detached from the rest of the garage chrome so the 2D
/// HUD layers (top bar, stats, crew chips) don't have to know anything about
/// <see cref="CameraView3D"/> arithmetic.
///
/// <see cref="Tick"/> advances the orbit angle; <see cref="Render"/> issues the actual
/// 3D pass. The renderer's BeginScene/EndScene bracket must wrap a single frame's draw
/// — calling it twice from the same frame is undefined behaviour at the renderer level.
/// </summary>
public sealed class OrbitingTankView
{
    private const float SpinRadPerSecond = 0.35f;
    private const float CameraDistanceFactor = 2.4f;
    private const float CameraHeightFactor = 0.55f;
    private const float CameraFovY = 38f;

    private readonly IModelLoader _modelLoader;
    private readonly IModelRenderer _modelRenderer;
    private readonly string _modelPath;

    private IModel? _tank;
    private double _spinSeconds;

    public OrbitingTankView(IModelLoader modelLoader, IModelRenderer modelRenderer, string modelPath)
    {
        _modelLoader = modelLoader;
        _modelRenderer = modelRenderer;
        _modelPath = modelPath;
    }

    /// <summary>True once the model has been loaded successfully (valid bounds). Drives
    /// the screen's "model failed to load" status line when false.</summary>
    public bool IsLoaded => _tank is { IsValid: true };

    public void Load()
    {
        _tank ??= _modelLoader.Load(_modelPath);
        _spinSeconds = 0;
    }

    public void Tick(double deltaSeconds)
    {
        _spinSeconds += deltaSeconds;
    }

    public void Render()
    {
        if (_tank is null || !_tank.IsValid)
        {
            return;
        }

        var size = _tank.BoundsMax - _tank.BoundsMin;
        var radius = Math.Max(Math.Max(size.X, size.Y), size.Z);
        var centre = (_tank.BoundsMin + _tank.BoundsMax) * 0.5f;
        var dist = radius * CameraDistanceFactor;
        var angle = (float)(_spinSeconds * SpinRadPerSecond);

        var camPos = centre + new Vector3(
            MathF.Cos(angle) * dist,
            dist * CameraHeightFactor,
            MathF.Sin(angle) * dist);
        var camera = CameraView3D.LookAt(camPos, centre, fovY: CameraFovY);

        _modelRenderer.BeginScene(camera);
        _modelRenderer.DrawModel(_tank, Vector3.Zero, 1f, Color.White);
        _modelRenderer.EndScene();
    }
}
