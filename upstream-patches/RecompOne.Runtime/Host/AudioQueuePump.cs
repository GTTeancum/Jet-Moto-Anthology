namespace RecompOne.Runtime.Host;

internal interface IAudioQueue
{
    bool IsPlaying { get; }
    int Processed { get; }
    int Queued { get; }
    void Stop();
    uint Unqueue();
    void FillAndQueue(uint buffer);
    void Play();
}

internal static class AudioQueuePump
{
    // A stopped source rewinds its entire queue on Play, including buffers
    // processed while we were mixing. Drain that queue before restarting it.
    internal static bool Service(IAudioQueue queue)
    {
        if (!queue.IsPlaying) return Reprime(queue);
        int processed = queue.Processed;
        for (int i = 0; i < processed; i++) queue.FillAndQueue(queue.Unqueue());
        return !queue.IsPlaying && Reprime(queue);
    }

    private static bool Reprime(IAudioQueue queue)
    {
        queue.Stop();
        int count = queue.Queued;
        if (count == 0) return false;
        if (count > 64) throw new InvalidOperationException("Unexpected audio queue size.");
        Span<uint> buffers = stackalloc uint[count];
        // Unqueue everything before adding any replacements, so even a backend
        // treating new stopped-source buffers as processed cannot recycle them.
        for (int i = 0; i < count; i++) buffers[i] = queue.Unqueue();
        foreach (uint buffer in buffers) queue.FillAndQueue(buffer);
        queue.Play();
        return true;
    }
}
