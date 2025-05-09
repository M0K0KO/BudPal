using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Ensures that actions from other threads (e.g., network callbacks)
/// are executed on Unity's main thread.
/// Unity API calls must be made from the main thread.
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance = null;
    private static bool _instanceExists = false; // To check if FindObjectOfType has been called

    /// <summary>
    /// Gets the singleton instance of the dispatcher.
    /// Creates the dispatcher GameObject if it doesn't exist.
    /// </summary>
    public static UnityMainThreadDispatcher Instance()
    {
        if (!_instanceExists && _instance == null) // Check only if not already found or created
        {
            _instance = FindObjectOfType<UnityMainThreadDispatcher>();
            _instanceExists = true; // Mark that we've tried to find it
            if (_instance == null)
            {
                GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher_AutoCreated");
                _instance = dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
                // Optionally, make it persist across scene loads
                // DontDestroyOnLoad(dispatcherObject);
                Debug.Log("UnityMainThreadDispatcher instance created.");
            }
        }
        return _instance;
    }

    void Awake()
    {
        // Ensure only one instance exists, especially if manually added to scene
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Another instance of UnityMainThreadDispatcher found. Destroying this one.");
            Destroy(gameObject);
            return;
        }
        if (_instance == null)
        {
            _instance = this;
            _instanceExists = true;
            // Optionally, make it persist across scene loads
            // DontDestroyOnLoad(gameObject);
        }
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                if (_executionQueue.Peek() != null) // Check if action is not null
                {
                    _executionQueue.Dequeue().Invoke();
                }
                else
                {
                    _executionQueue.Dequeue(); // Remove null action
                    Debug.LogWarning("Dequeued a null action from UnityMainThreadDispatcher.");
                }
            }
        }
    }

    /// <summary>
    /// Enqueues an action to be executed on the main thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Enqueue(Action action)
    {
        if (action == null)
        {
            Debug.LogWarning("Attempted to enqueue a null action.");
            return;
        }
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            _instanceExists = false; // Reset flag if this instance is destroyed
        }
    }
}