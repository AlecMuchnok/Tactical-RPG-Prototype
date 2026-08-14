using System;
using UnityEngine;

/// <summary>Cross-system signal carrying which Team's turn it now is — drives the turn banner.</summary>
[CreateAssetMenu(menuName = "Game/Event Channels/Team Channel")]
public sealed class TeamEventChannel : ScriptableObject
{
    public event Action<Team> Raised;

    public void Raise(Team team) => Raised?.Invoke(team);
}
