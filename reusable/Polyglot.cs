using System.Linq;
using System.Collections.Generic;

using Utils.Polyglot.PluralTypes;

namespace Utils.Polyglot
{
	public class Plural
	{
		public static Dictionary<string, Plural> PluralStrategies;
		public static German German;
		public static French French;
		public static Chinese Chinese;
		public static Russian Russian;
		public static Czech Czech;
		public static Polish Polish;
		public static Icelandic Icelandic;

		static Plural ()
		{
			PluralStrategies = new Dictionary<string, Plural> ();
			German = new German ();
			French = new French ();
			Chinese = new Chinese ();
			Russian = new Russian ();
			Czech = new Czech ();
			Polish = new Polish ();
			Icelandic = new Icelandic ();

			PluralStrategies["fa"] = Chinese;
			PluralStrategies["id"] = Chinese;
			PluralStrategies["ja"] = Chinese;
			PluralStrategies["ko"] = Chinese;
			PluralStrategies["lo"] = Chinese;
			PluralStrategies["ms"] = Chinese;
			PluralStrategies["th"] = Chinese;
			PluralStrategies["tr"] = Chinese;
			PluralStrategies["zh"] = Chinese;
			PluralStrategies["da"] = German;
			PluralStrategies["de"] = German;
			PluralStrategies["en"] = German;
			PluralStrategies["es"] = German;
			PluralStrategies["fi"] = German;
			PluralStrategies["el"] = German;
			PluralStrategies["he"] = German;
			PluralStrategies["hu"] = German;
			PluralStrategies["it"] = German;
			PluralStrategies["nl"] = German;
			PluralStrategies["no"] = German;
			PluralStrategies["pt"] = German;
			PluralStrategies["sv"] = German;
			PluralStrategies["fr"] = French;
			PluralStrategies["tl"] = French;
			PluralStrategies["pt-br"] = French;
			PluralStrategies["hr"] = Russian;
			PluralStrategies["ru"] = Russian;
			PluralStrategies["cs"] = Czech;
			PluralStrategies["pl"] = Polish;
			PluralStrategies["is"] = Icelandic;
		}

		public virtual int GetEntryIndexForCount (int N)
		{
			return 0;
		}
	}

	// алгоритмы применения вариантов множественного числа
	namespace PluralTypes
	{
		public class Chinese : Plural
		{
		}

		public class German : Plural
		{
			public override int GetEntryIndexForCount (int N)
			{
				return N != 1 ? 1 : 0;
			}
		}

		public class French : Plural
		{
			public override int GetEntryIndexForCount (int N)
			{
				return N > 1 ? 1 : 0;
			}
		}

		public class Russian : Plural
		{
			public override int GetEntryIndexForCount (int N)
			{
				return N % 10 == 1 && N % 100 != 11
					? 0
					: N % 10 >= 2 && N % 10 <= 4 && (N % 100 < 10 || N % 100 >= 20)
						? 1
						: 2
					;
			}
		}

		public class Czech : Plural
		{
			public override int GetEntryIndexForCount (int N)
			{
				return (N == 1)
					? 0
					: (N >= 2 && N <= 4)
						? 1
						: 2
					;
			}
		}

		public class Polish : Plural
		{
			public override int GetEntryIndexForCount (int N)
			{
				return N == 1
					? 0
					: N % 10 >= 2 && N % 10 <= 4 && (N % 100 < 10 || N % 100 >= 20)
						? 1
						: 2
					;
			}
		}

		public class Icelandic : Plural
		{
			public override int GetEntryIndexForCount (int N)
			{
				return N % 10 != 1 || N % 100 == 11 ? 1 : 0;
			}
		}
	}

	// select the due plural for the integer
	public class PluralIndexer
	{
		string[] Values;
		Plural Plural;

		public static PluralIndexer Dummy = new PluralIndexer (null, null);

		public PluralIndexer (string[] Values, Plural Plural)
		{
			this.Values = Values;
			this.Plural = Plural;
		}

		public string this [int Count]
		{
			get
			{
				if (Plural == null || Values == null || Values.Length == 0)
				{
					return "";
				}

				int Index = Plural.GetEntryIndexForCount (Count);

				if (Index >= Values.Length)
				{
					Index = 0;
				}

				return Values[Index];
			}
		}
	}

	public static class Utils
	{
		// search for the record in the dictionary of languages
		// if "xx-yy" is missing, look for "xx"
		public static T GetForLanguage<T> (this IDictionary<string, T> Dict, string[] langlist, T Default)
		{
			if (langlist == null || langlist.Length == 0)
			{
				return Default;
			}

			langlist = langlist.Select (l => l.ToLower ()).ToArray ();

			T Result;

			// full names first
			foreach (string lang in langlist)
			{
				if (Dict.TryGetValue (lang, out Result))
				{
					return Result;
				}
			}

			// now cut, 2-letter
			foreach (string lang in langlist
										.Where (l => l.Length > 2 && l[2] == '-')
										.Select (l => l.Substring (0, 2))
					)
			{
				if (Dict.TryGetValue (lang, out Result))
				{
					return Result;
				}
			}

			return Default;
		}

		// look for the record in the dictionary of languages
		// if "xx-yy" is missing, look for "xx"
		public static T GetForLanguage<T> (this IDictionary<string, T> Dict, string lang, T Default)
		{
			return GetForLanguage (Dict, new[] { lang }, Default);
		}
	}
}
