﻿

namespace Infrastructure.Throttling
{
    public sealed class RateLimiter
    {
        internal const string RATELIMIT_LIMIT = "RateLimit-Limit";
        internal const string RATELIMIT_REMAINING = "RateLimit-Remaining";
        internal const string RATELIMIT_RESET = "RateLimit-Reset";

        /// <summary>
        /// Lock for controlling Read/Write access to the variables.
        /// </summary>
        private readonly ReaderWriterLockSlim readerWriterLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Maximum number of requests per window
        /// </summary>
        private int limit;

        /// <summary>
        /// The time, in <see cref="TimeSpan.Seconds"/>, when the current window gets reset
        /// </summary>
        private int reset;

        /// <summary>
        /// The timestamp when current window will be reset, in <see cref="TimeSpan.Ticks"/>.
        /// </summary>
        private long nextReset;

        /// <summary>
        /// The remaining requests in the current window.
        /// </summary>
        private int remaining;

        /// <summary>
        /// Minimum % of requests left before the next request will get delayed until the current window is reset
        /// Feel free to experiment with this number to find the optimal value for your scenario
        /// </summary>
        internal int MinimumCapacityLeft { get; set; } = 10;

        /// <summary>
        /// Default constructor
        /// </summary>
        public RateLimiter()
        {
            readerWriterLock.EnterWriteLock();
            try
            {
                _ = Interlocked.Exchange(ref limit, -1);
                _ = Interlocked.Exchange(ref remaining, -1);
                _ = Interlocked.Exchange(ref reset, -1);
                _ = Interlocked.Exchange(ref nextReset, DateTime.UtcNow.Ticks);
            }
            finally
            {
                readerWriterLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// If needed delay the execution of an HTTP request
        /// </summary>
        /// <param name="apiType">Type of API that we're possibly delaying (for logging purposes only)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        internal async Task WaitAsync(string apiType, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // We're not using the rate limiter
            if (MinimumCapacityLeft == 0)
            {
                return;
            }

            long delayInTicks = 0;
            float capacityLeft = 0;
            readerWriterLock.EnterReadLock();
            try
            {
                // Remaining = 0 means the request is throttled and there's a retry-after header that will be used
                if (limit > 0 && remaining > 0)
                {
                    // Calculate percentage requests left in the current window
                    capacityLeft = (float)Math.Round((float)remaining / limit * 100, 2);

                    // If getting below the minimum required capacity then lets wait until the current window is reset
                    if (capacityLeft <= MinimumCapacityLeft)
                    {
                        delayInTicks = nextReset - DateTime.UtcNow.Ticks;
                    }
                }
            }
            finally
            {
                readerWriterLock.ExitReadLock();
            }

            if (delayInTicks > 0)
            {
                Console.WriteLine($"[orange3]Delaying {apiType} request for {new TimeSpan(delayInTicks).Seconds} seconds, capacity left: {capacityLeft}%[/]");

                await Task.Delay(new TimeSpan(delayInTicks), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Checks for RateLimit headers and if so processes them
        /// </summary>
        /// <param name="response">Respose from the HTTP request</param>
        /// <param name="apiType">Type of API that we're possibly delaying (for logging purposes only)</param>
        internal void UpdateWindow(HttpResponseMessage response, string apiType)
        {
            int rateLimit = -1;
            int rateRemaining = -1;
            int rateReset = -1;

            // We're not using the rate limiter
            if (MinimumCapacityLeft == 0)
            {
                return;
            }

            if (response != null)
            {
                if (response.Headers.TryGetValues(RATELIMIT_LIMIT, out IEnumerable<string> limitValues))
                {
                    string rateString = limitValues.First();
                    _ = int.TryParse(rateString, out rateLimit);
                }

                if (response.Headers.TryGetValues(RATELIMIT_REMAINING, out IEnumerable<string> remainingValues))
                {
                    string rateString = remainingValues.First();
                    _ = int.TryParse(rateString, out rateRemaining);
                }

                if (response.Headers.TryGetValues(RATELIMIT_RESET, out IEnumerable<string> resetValues))
                {
                    string rateString = resetValues.First();
                    _ = int.TryParse(rateString, out rateReset);
                }

                readerWriterLock.EnterWriteLock();
                try
                {
                    _ = Interlocked.Exchange(ref limit, rateLimit);
                    _ = Interlocked.Exchange(ref remaining, rateRemaining);
                    _ = Interlocked.Exchange(ref reset, rateReset);

                    if (rateReset > -1)
                    {
                        // Track when the current window get's reset
                        _ = Interlocked.Exchange(ref nextReset, DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(rateReset).Ticks);
                    }
                }
                finally
                {
                    readerWriterLock.ExitWriteLock();
                }

                if (rateReset > -1)
                {
                    Console.WriteLine($"{apiType} request. RateLimit-Limit: {rateLimit}, RateLimit-Remaining: {rateRemaining}, RateLimit-Reset: {rateReset}");
                }
            }
        }

    }
}
