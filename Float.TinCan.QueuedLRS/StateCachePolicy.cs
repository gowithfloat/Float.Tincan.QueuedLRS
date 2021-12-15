namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// Describes the various cache policies that can be used for retreiving remote state.
    /// </summary>
    public enum StateCachePolicy
    {
        /// <summary>
        /// Only return once the remote endpoint has responded.
        /// This was the behavior before the ability to change state cache policies was implemented.
        /// </summary>
        PreferRemote,

        /// <summary>
        /// Always return the local state cache, without communicating to the remote endpoint.
        /// Note that using this continuously will not allow the user to restore state from another device.
        /// Also note that, even in this mode, if the content doesn't exist locally, it will be retrieved from the server.
        /// </summary>
        PreferLocal,

        /// <summary>
        /// Return the local cached state, and update from the remote endpoint in the background.
        /// While this might cause the current state request to retrieve old data, a future state request would get the latest data from the server.
        /// </summary>
        KeepLocalUpdated,
    }
}
