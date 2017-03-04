using System;
using System.Collections.Generic;

public class EventManager : IDisposable
{
    protected Dictionary<uint, Action<Message>> callBackMap;
    protected Dictionary<string, List<Action<Message>>> eventMap;

    public EventManager()
    {
        callBackMap = new Dictionary<uint, Action<Message>>();
        eventMap = new Dictionary<string, List<Action<Message>>>();
    }

    public void ClearCallBackMap()
    {
        callBackMap.Clear();
    }

    public void ClearEventMap()
    {
        eventMap.Clear();
    }

    public int GetCallbackCount()
    {
        return callBackMap.Count;
    }

    //Adds callback to callBackMap by id.
    public void AddCallback(uint id, Action<Message> callback)
    {
        UnityEngine.Debug.Assert(callback != null);
        UnityEngine.Debug.Assert(id > 0);

        callBackMap.Add(id, callback);
    }

    public void RemoveCallback(uint id)
    {
        callBackMap.Remove(id);
    }

    /// <summary>
    /// Invoke the callback when the server return messge.
    /// </summary>
    /// <param name='pomeloMessage'>
    /// Pomelo message.
    /// </param>
    public void InvokeCallBack(uint id, Message data)
    {
        if (!callBackMap.ContainsKey(id)) return;
        callBackMap[id].Invoke(data);
    }

    public void RemoveOnEvent(string eventName)
    {
        eventMap.Remove(eventName);
    }

    //Adds the event to eventMap by name.
    public void AddOnEvent(string eventName, Action<Message> callback)
    {
        List<Action<Message>> list = null;
        if (eventMap.TryGetValue(eventName, out list))
        {
            list.Add(callback);
        }
        else
        {
            list = new List<Action<Message>>();
            list.Add(callback);
            eventMap.Add(eventName, list);
        }
    }

    /// <summary>
    /// If the event exists,invoke the event when server return messge.
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    ///
    public void InvokeOnEvent(string route, Message msg)
    {
        if (!eventMap.ContainsKey(route)) return;

        List<Action<Message>> list = eventMap[route];
        foreach (Action<Message> action in list) action.Invoke(msg);
    }

    // Dispose() calls Dispose(true)
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // The bulk of the clean-up code is implemented in Dispose(bool)
    protected void Dispose(bool disposing)
    {
        callBackMap.Clear();
        eventMap.Clear();
    }
}
