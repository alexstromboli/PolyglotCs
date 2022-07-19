using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Utils.Polyglot;
using Utils.CodeGeneration;

namespace Compile
{
	class Config
	{
		public string default_locale;
		public string project_name;
		public JObject defaults;
	}

	class LanguageEntry
	{
		public string SingleValue;
		public string[] PluralValues;
		public string RawValue;

		public bool IsSingle => PluralValues == null;

		public static LanguageEntry FromRawValue (string Value)
		{
			if (Value == null || !Value.Contains (Program.PluralDelimiter))
			{
				return new LanguageEntry
				{
					SingleValue = Value,
					RawValue = Value
				};
			}

			string[] PluralValues = Value.Split (Program.PluralDelimiter);

			if (PluralValues.Length > 3)
			{
				PluralValues = PluralValues.Take (3).ToArray ();
			}

			return new LanguageEntry
			{
				PluralValues = PluralValues,
				RawValue = Value
			};
		}
	}

	class Language
	{
		public string Icon;
		public string ClassTitle;
		public string InstanceTitle => ClassTitle + "_";
		public Dictionary<string, LanguageEntry> Entries;
	}

	class Program
	{
		public static readonly string PluralDelimiter = "||||";

		static string NormMethodName (string Input)
		{
			return Input.Replace (".", "__");
		}

		public static string StringLiteral (string Input, bool ForceNotNull = false)
		{
			if (Input == null && ForceNotNull)
			{
				Input = "";
			}

			if (Input == null)
			{
				return "null";
			}

			return new StringBuilder ()
				.Append ('"')
				.Append (Input
						.Replace ("\"", "\\\"")
						.Replace ("\r", "\\r")
						.Replace ("\n", "\\n")
					)
				.Append ('"')
				.ToString ()
				;
		}

		static void FlattenEntries (JObject Data, Dictionary<string, string> TargetDict, string Prefix = null)
		{
			Prefix = Prefix == null
					? ""
					: Prefix + "."
				;

			foreach (JProperty p in Data.Properties ())
			{
				string Name = Prefix + p.Name;

				if (p.Value.Type == JTokenType.String)
				{
					string Value = p.Value.Value<string> ();
					TargetDict[Name] = Value;
				}
				else if (p.Value.Type == JTokenType.Object)
				{
					JObject Value = p.Value.Value<JObject> ();
					FlattenEntries (Value, TargetDict, Name);
				}
			}
		}

		static void Main (string[] args_raw)
		{
			Utils.Args.Args args = new Utils.Args.Args (args_raw);

			string OutputDir = args.GetAndExcludeParam ("out") ?? ".";
			OutputDir = Path.GetFullPath (OutputDir);

			string FlatTsDir = args.GetAndExcludeParam ("flatts");
			FlatTsDir = FlatTsDir == null ? null : Path.GetFullPath (FlatTsDir);

			string ConfigPath = "config.json";
			if (args.Count > 0)
			{
				ConfigPath = args[0];
			}

			ConfigPath = Path.GetFullPath (ConfigPath);
			string ConfigDir = Path.GetDirectoryName (ConfigPath);

			//
			Config Config = JsonConvert.DeserializeObject<Config> (File.ReadAllText (ConfigPath));
			Config.default_locale = Config.default_locale ?? "en";
			string ProjectNamespace = null;
			string ProjectLocalName = Config.project_name ?? "Utils.Polyglot.Languages";
			Match mNamespace = Regex.Match (ProjectLocalName, @"\.(?<name>[^\.]+)$");
			if (mNamespace.Success)
			{
				ProjectNamespace = ProjectLocalName.Substring (0, mNamespace.Index);
				ProjectLocalName = mNamespace.Groups["name"].Value;
			}

			// language dependencies
			// primary to dependants
			List<string> LangOrder = new List<string> ();
			// all dependencies, including implicit
			// null for default_locale
			Dictionary<string,string> DefaultsDict = null;

			LangOrder.Add (Config.default_locale);

			if (Config.defaults == null)
			{
				DefaultsDict = new Dictionary<string, string> ();
			}
			else
			{
				DefaultsDict = Config.defaults
						.Properties ()
						.ToDictionary (p => p.Name, p => p.Value.Value<string> ())
					;

				// restore the languages order
				// for all mentioned in DefaultsDict, left or right

				// first the right,
				// which don't refer on their own
				while (true)
				{
					string Refd = DefaultsDict.Values
							.FirstOrDefault (s =>
								!LangOrder.Contains (s)
								&& s != Config.default_locale
								&& (!DefaultsDict.ContainsKey (s)
									|| LangOrder.Contains (DefaultsDict[s]))
								)
						;

					if (Refd == null)
					{
						break;
					}

					LangOrder.Add (Refd);

					if (!DefaultsDict.ContainsKey (Refd))
					{
						DefaultsDict[Refd] = Config.default_locale;
					}
				}

				// the remaining on the right are loops
				// direct them all to default_locale
				foreach (string lang in DefaultsDict.Values
					.Where (s =>
						!LangOrder.Contains (s)
						&& s != Config.default_locale
					)
					.Distinct ()
					.ToArray ())
				{
					DefaultsDict[lang] = Config.default_locale;
					LangOrder.Add (lang);
				}

				// then all the remaining on the left
				while (true)
				{
					string Refd = DefaultsDict.Keys
							.FirstOrDefault (s => !LangOrder.Contains (s)
									&& s != Config.default_locale)
						;

					if (Refd == null)
					{
						break;
					}

					LangOrder.Add (Refd);
				}
			}

			DefaultsDict[Config.default_locale] = null;
			Dictionary<string, Language> LanguagesDict = new Dictionary<string, Language> ();

			// enum files
			foreach (string LangFilePath in Directory.GetFiles (ConfigDir, "*.json"))
			{
				string LangIcon = Path.GetFileNameWithoutExtension (LangFilePath).ToLower ();

				if (!Regex.Match (LangIcon, @"^[a-z]{2}(\-[a-z]{2})?$").Success)
				{
					continue;
				}

				JObject LangJson = JsonConvert.DeserializeObject<JObject> (File.ReadAllText (LangFilePath, Encoding.UTF8));
				Dictionary<string,string> Entries = new Dictionary<string, string> ();
				FlattenEntries (LangJson, Entries);

				Language Language = new Language
				{
					Icon = LangIcon,
					Entries = Entries.ToDictionary (en => en.Key, en => LanguageEntry.FromRawValue (en.Value))
				};

				LanguagesDict[Language.Icon] = Language;

				//
				if (!DefaultsDict.ContainsKey (Language.Icon))
				{
					DefaultsDict[Language.Icon] = Config.default_locale;
				}

				if (!LangOrder.Contains (Language.Icon))
				{
					LangOrder.Add (Language.Icon);
				}
			}

			// build interface
			// name --> issingle
			Dictionary<string,bool> AllMethodsDict = new Dictionary<string, bool> ();
			foreach (string lang in LangOrder)
			{
				if (!LanguagesDict.TryGetValue (lang, out var Language))
				{
					continue;
				}

				foreach (var kvp in Language.Entries)
				{
					if (AllMethodsDict.ContainsKey (kvp.Key))
					{
						continue;
					}

					AllMethodsDict[kvp.Key] = kvp.Value.IsSingle;
				}
			}

			//
			Directory.CreateDirectory (OutputDir);

			//
			string InterfaceTitle = "ILocale";
			string ProjectClassFilePath = Path.Combine (OutputDir, ProjectLocalName + ".cs");

			// interface
			IndentedTextBuilder sbProjectClass = new IndentedTextBuilder ()
				.AppendLine (CodeGenerationUtils.AutomaticWarning)
				.AppendLine ("using System.Collections.Generic;")
				.AppendLine ()
				.AppendLine ("using Utils.Polyglot;")
				.AppendLine ()
				;

			using (ProjectNamespace == null
					? null
					: sbProjectClass
						.AppendFormat ("namespace {0}", ProjectNamespace)
						.UseCurlyBraces ()
					)
			{
				using (sbProjectClass
						.AppendFormat ("public partial class {0}", ProjectLocalName)
						.UseCurlyBraces ()
					)
				{
					using (sbProjectClass
							.AppendFormat ("public interface {0}", InterfaceTitle)
							.UseCurlyBraces ()
						)
					{
						foreach (string MethodName in AllMethodsDict.Keys.OrderBy (s => s))
						{
							sbProjectClass.AppendFormat ("{0} {1} {{ get; }}",
									AllMethodsDict[MethodName] ? "string" : "PluralIndexer",
									NormMethodName (MethodName)
								);
						}
					}

					// the class will be be done after language classes

					// language files
					Dictionary<string, string> PluralStrategies = new Dictionary<string, string>
					{
						["fa"] = "Chinese",
						["id"] = "Chinese",
						["ja"] = "Chinese",
						["ko"] = "Chinese",
						["lo"] = "Chinese",
						["ms"] = "Chinese",
						["th"] = "Chinese",
						["tr"] = "Chinese",
						["zh"] = "Chinese",
						["da"] = "German",
						["de"] = "German",
						["en"] = "German",
						["es"] = "German",
						["fi"] = "German",
						["el"] = "German",
						["he"] = "German",
						["hu"] = "German",
						["it"] = "German",
						["nl"] = "German",
						["no"] = "German",
						["pt"] = "German",
						["sv"] = "German",
						["fr"] = "French",
						["tl"] = "French",
						["pt-br"] = "French",
						["hr"] = "Russian",
						["ru"] = "Russian",
						["cs"] = "Czech",
						["pl"] = "Polish",
						["is"] = "Icelandic"
					};

					foreach (string lang in LangOrder)
					{
						if (!LanguagesDict.TryGetValue (lang, out var Language))
						{
							Language = new Language
							{
								Icon = lang,
								Entries = new Dictionary<string, LanguageEntry> ()
							};

							LanguagesDict[lang] = Language;
						}

						Language.ClassTitle = Language.Icon.Replace ("-", "_");

						//
						string Plural = PluralStrategies.GetForLanguage (lang, "Plural");

						// by default
						// must be there, must have already been processed
						// before by LangOrder
						string DefaultIcon = DefaultsDict[lang];
						string DefaultClassName = DefaultIcon == null ? null : LanguagesDict[DefaultIcon].ClassTitle;
						string DefaultInstanceName =
							DefaultClassName == null ? null : LanguagesDict[DefaultIcon].InstanceTitle;

						//
						string ClassFilePath = Path.Combine (OutputDir, Language.Icon + ".cs");

						// interface
						IndentedTextBuilder sbClass = new IndentedTextBuilder ()
								.AppendLine (CodeGenerationUtils.AutomaticWarning)
								.AppendLine ("using Utils.Polyglot;")
								.AppendLine ()
							;

						using (ProjectNamespace == null
								? null
								: sbClass.AppendFormat ("namespace {0}", ProjectNamespace)
									.UseCurlyBraces ()
							)
						{
							using (sbClass.AppendFormat ("public partial class {0}", ProjectLocalName)
										.UseCurlyBraces ())
							{
								using (sbClass.AppendFormat ("public class {0} : {1}", Language.ClassTitle,
												InterfaceTitle)
											.UseCurlyBraces ())
								{
									Dictionary<string, string> PredefinedIndexers = new Dictionary<string, string> ();

									foreach (string MethodName in AllMethodsDict.Keys.OrderBy (s => s))
									{
										LanguageEntry LanguageEntry = null;
										Language.Entries.TryGetValue (MethodName, out LanguageEntry);

										string CsMethodName = NormMethodName (MethodName);
										bool IsSingle = AllMethodsDict[MethodName];

										string PredefinedIndexer = "PLURIND_" + CsMethodName;
										if (!IsSingle && LanguageEntry?.PluralValues != null)
										{
											PredefinedIndexers[PredefinedIndexer] = string.Format (
												"new PluralIndexer (new[] {{ {0} }}, Plural.{1})",
												string.Join (", ", LanguageEntry.PluralValues
													.Select (s => StringLiteral (s))),
												Plural);
										}

										sbClass.AppendFormat ("public {0} {1} => {2};",
												IsSingle ? "string" : "PluralIndexer",
												CsMethodName,
												IsSingle
													? (LanguageEntry?.SingleValue == null
														? (DefaultClassName == null
															? "\"\""
															: $"{DefaultInstanceName}.{CsMethodName}")
														: StringLiteral (LanguageEntry.SingleValue))
													: (LanguageEntry?.PluralValues == null
														? (DefaultClassName == null
															? "PluralIndexer.Dummy"
															: $"{DefaultInstanceName}.{CsMethodName}")
														: PredefinedIndexer)
											);
									}

									if (PredefinedIndexers.Count > 0)
									{
										sbClass.AppendLine ();

										foreach (var kvp in PredefinedIndexers.OrderBy (p => p.Key))
										{
											sbClass.AppendFormat ("static readonly PluralIndexer {0} = {1};",
													kvp.Key,
													kvp.Value
												);
										}
									}
								}

								sbClass
									.AppendLine ()
									.AppendFormat ("public static {0} {1} = new {0} ();",
										Language.ClassTitle,
										Language.InstanceTitle)
									;
							}
						}

						CodeGenerationUtils.EnsureFileContents (ClassFilePath, sbClass.ToString (), EndOfLine.MakeCrLf,
							Encoding.UTF8);
					}

					// finalize the primary class

					sbProjectClass
						.AppendLine ()
						.AppendLine ("static Dictionary<string, ILocale> Map;")
						.AppendFormat ("static ILocale DefaultLocale = {0};",
							LanguagesDict[Config.default_locale].InstanceTitle)
						.AppendLine ()
						;

					using (sbProjectClass.AppendFormat ("static {0} ()", ProjectLocalName)
						.UseCurlyBraces ())
					{
						sbProjectClass.AppendLine ("Map = new Dictionary<string, ILocale> ();");

						foreach (var kvp in LanguagesDict.OrderBy (p => p.Key))
						{
							sbProjectClass
								.AppendFormat ("Map[\"{0}\"] = {1};", kvp.Key, kvp.Value.InstanceTitle)
								;
						}
					}

					sbProjectClass.AppendLine ();

					using (sbProjectClass.AppendLine ("public static ILocale GetForLanguage (string lang)")
						.UseCurlyBraces ())
					{
						sbProjectClass.AppendLine ("return Map.GetForLanguage (lang, DefaultLocale);");
					}

					sbProjectClass.AppendLine ();

					using (sbProjectClass.AppendLine ("public static ILocale GetForLanguage (string[] langlist)")
						.UseCurlyBraces ())
					{
						sbProjectClass.AppendLine ("return Map.GetForLanguage (langlist, DefaultLocale);");
					}
				}
			}

			CodeGenerationUtils.EnsureFileContents (ProjectClassFilePath, sbProjectClass.ToString (), EndOfLine.MakeCrLf, Encoding.UTF8);

			// flat typescript
			if (FlatTsDir != null)
			{
				Directory.CreateDirectory (FlatTsDir);

				foreach (var Language in LanguagesDict.Values.Where (l => l.Entries.Count > 0))
				{
					JObject EntriesObj = new JObject (Language.Entries
						.OrderBy (en => en.Key)
						.Select (en => new JProperty (en.Key, en.Value.RawValue)));
					string Json = JsonConvert.SerializeObject (EntriesObj);

					string TsPath = Path.Combine (FlatTsDir, Language.Icon + ".ts");
					CodeGenerationUtils.EnsureFileContents (TsPath, Json, EndOfLine.MakeCrLf, Encoding.UTF8);
				}
			}
		}
	}
}
