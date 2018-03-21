using System;
using System.Collections.Concurrent;
using System.Linq;
using AngleSharp.Dom;
using PreMailer.Net.Downloaders;

namespace PreMailer.Net.Sources
{
    // NOTE: this breaks tests since the tests assume the same Uri will 
    // return many different strings (Which it supplies)...
    internal static class LinkTagCssSourceCache
    {
        static ConcurrentDictionary<string, Tuple<DateTimeOffset, string>> Cache = new ConcurrentDictionary<string, Tuple<DateTimeOffset, string>>();
        static TimeSpan CssTimeout = TimeSpan.FromMinutes(15);
        static TimeSpan CacheTimeout = TimeSpan.FromDays(1);
        static DateTimeOffset CacheExpiryTime = DateTimeOffset.UtcNow.Add(CacheTimeout);

        public static string GetOrAdd(string key, Func<string, string> valueFunc)
        {
            if (DateTimeOffset.UtcNow > CacheExpiryTime)
            {
                CacheExpiryTime = DateTimeOffset.UtcNow.Add(CacheTimeout);
                Cache = new ConcurrentDictionary<string, Tuple<DateTimeOffset, string>>();
            }

            Tuple<DateTimeOffset, string> val = null;
            if (Cache.TryGetValue(key, out val))
            {
                if (val.Item1 > DateTimeOffset.UtcNow)
                {
                    return val.Item2;
                }
            }

            var addValue = valueFunc(key);
            Cache.TryAdd(key, Tuple.Create(DateTimeOffset.UtcNow.Add(CssTimeout), addValue));
            return addValue;
        }
    }

	public class LinkTagCssSource : ICssSource
	{
		private readonly Uri _downloadUri;
		private string _cssContents;

		public LinkTagCssSource(IElement node, Uri baseUri)
		{
			// There must be an href
			var href = node.Attributes.First(a => a.Name.Equals("href", StringComparison.OrdinalIgnoreCase)).Value;

			if (Uri.IsWellFormedUriString(href, UriKind.Relative) && baseUri != null)
			{
				_downloadUri = new Uri(baseUri, href);
			}
			else
			{
				// Assume absolute
				_downloadUri = new Uri(href);
			}
		}

		public string GetCss()
		{
			if (IsSupported(_downloadUri.Scheme))
			{
                return _cssContents ?? (_cssContents =
                    LinkTagCssSourceCache.GetOrAdd(
                        _downloadUri.AbsoluteUri,
                        (uri) => WebDownloader.SharedDownloader.DownloadString(_downloadUri)
                    )
                );
			}
			return string.Empty;
		}

		private bool IsSupported(string scheme)
		{
			return
				scheme == "http" ||
				scheme == "https" ||
				scheme == "ftp" ||
				scheme == "file";
		}
	}
}