using System;

namespace TestApp
{
	class Program
	{
		static void Main (string[] args)
		{
			Languages.ILocale en = Languages.GetForLanguage ("en");
			Languages.ILocale ru = Languages.GetForLanguage ("ru");
			Languages.ILocale xx = Languages.GetForLanguage ("it-xx");
			Languages.ILocale yy = Languages.GetForLanguage ("yy");
			Languages.ILocale zz = Languages.GetForLanguage (new[] { "zz-UA", "zz-us", "ru-RU", "ru-ua" });
			Languages.ILocale ww = Languages.GetForLanguage (new[] { "zz-UA", "zz-us", "it", "ru-RU", "ru-ua" });

			foreach (var loc in new[] {ru, en, xx, yy, zz, ww})
			foreach (int n in new[] {3, 6, 21})
			{
				Console.WriteLine ("{0} {1}", n, loc.days_ago[n]);
			}
		}
	}
}
