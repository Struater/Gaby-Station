// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Client._Dumont.Scanning;

/// <summary>
/// Tracks the scan effect on a target sprite.
/// </summary>
[RegisterComponent]
public sealed partial class ScanalyzerTargetComponent : Component
{
    public EntityUid Scanner;

    public float Ratio;
}
