using System;

namespace LvlUp.RemoteConfig
{
    /// <summary>
    /// Lets one config fetch run at a time without losing the requests made while it runs. A request
    /// that arrives mid-fetch carries new request state (an advertising ID, a user ID) that the
    /// running fetch was built without, so it is folded into a single follow-up fetch instead of
    /// being dropped.
    /// </summary>
    internal class RemoteConfigFetchGate
    {
        private bool _isFollowUpRequested;
        private Action<bool> _followUpOnComplete;

        public bool IsFetching { get; private set; }

        /// <summary>
        /// True when the caller should start a fetch now. False when one is running; the request then
        /// joins the follow-up fetch and <paramref name="onComplete"/> is invoked when that ends.
        /// </summary>
        public bool TryBegin(Action<bool> onComplete)
        {
            if (IsFetching)
            {
                _isFollowUpRequested = true;
                _followUpOnComplete += onComplete;
                return false;
            }

            IsFetching = true;
            return true;
        }

        /// <summary>
        /// Ends the running fetch. True when a follow-up was requested meanwhile: it has already begun,
        /// so the caller must start it, and <paramref name="followUpOnComplete"/> holds its callbacks.
        /// </summary>
        public bool End(out Action<bool> followUpOnComplete)
        {
            followUpOnComplete = null;

            if (!_isFollowUpRequested)
            {
                IsFetching = false;
                return false;
            }

            followUpOnComplete = _followUpOnComplete;
            _followUpOnComplete = null;
            _isFollowUpRequested = false;
            return true;
        }
    }
}
