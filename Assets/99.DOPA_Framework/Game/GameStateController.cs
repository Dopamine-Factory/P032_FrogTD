using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public enum GameState { Home, Resume, Paused, GameStart, GameOver, GameClear }
public class GameStateEvent
{
    public Action Callback;
}

public class GameStateController
{

    Dictionary<GameState, Action> subscribes = new Dictionary<GameState, Action>();

    public GameStateEvent OnStateChanged = new();
    public GameState CurrentState { get; private set; } = GameState.Home;

    public void Subscribe(GameState state, Action callback)
    {
        if (subscribes.ContainsKey(state) == false)
        {
            subscribes[state] = null;
        }
        subscribes[state] += callback;
    }

    public void Unsubscribe(GameState state, Action callback)
    {
        if (subscribes.ContainsKey(state) == false) return;

        subscribes[state] -= callback;
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;

        subscribes[newState]?.Invoke();
    }

}