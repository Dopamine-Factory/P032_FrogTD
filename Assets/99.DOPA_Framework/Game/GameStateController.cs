using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public class GameStateController
{
    public enum GameState { Home, Resume, Paused, GameStart, GameOver, GameClear }
    public class GameStateEvent
    {
        public Action Callback;
    }

    Dictionary<GameState, Action> subscribes = new Dictionary<GameState, Action>();

    public GameStateEvent OnStateChanged = new();
    public GameState CurrentState { get; private set; } = GameState.Home;

    public void Subscribe(GameState state, Action callback)
    {
        subscribes[state] += callback;
    }

    public void Unsubscribe(GameState state, Action callback)
    {
        subscribes[state] -= callback;
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;

        subscribes[newState]?.Invoke();
    }

}