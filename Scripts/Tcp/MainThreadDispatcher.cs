using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System;

public class MainThreadDispatcher : MonoBehaviour
{
    public static MainThreadDispatcher Instance { get; private set; }
    private readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        while (actions.TryDequeue(out var a))
        {
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogError("MainThreadDispatcher action异常: " + e); }
        }
    }

    // ★任何线程都能调用
    public static void Enqueue(Action action)
    {
        if (action == null) return;

        var inst = Instance;
        if (inst == null)
        {
            Debug.LogError("场景中未找到 MainThreadDispatcher 实例，请手动添加！");
            return;
        }

        inst.actions.Enqueue(action);
    }
}