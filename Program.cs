﻿using Mono.Options;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Console;

namespace jittimes {
	class MainClass {
		static bool Verbose;
		static readonly string Name = "jit-times";
		static readonly List<Regex> methodNameRegexes = new List<Regex> ();

		enum SortKind {
			Unsorted,
			Self,
			Total,
		};

		static SortKind sortKind = SortKind.Self;

		static string ProcessArguments (string [] args)
		{
			var help = false;
			var options = new OptionSet {
				$"Usage: {Name}.exe OPTIONS* <methods-file>",
				"",
				"Processes JIT methods file from XA app with debug.mono.log=timing enabled",
				"",
				"Copyright 2019 Microsoft Corporation",
				"",
				"Options:",
				{ "h|help|?",
					"Show this message and exit",
				  v => help = v != null },
				{ "m|method=",
					"Process only methods whose names match {TYPE-REGEX}.",
				  v => methodNameRegexes.Add (new Regex (v)) },
				{ "s",
					"Sort by self times. (this is default ordering)",
				  v => sortKind = SortKind.Self },
				{ "t",
					"Sort by total times.",
				  v => sortKind = SortKind.Total },
				{ "u",
					"Show unsorted results.",
				  v => sortKind = SortKind.Unsorted },
				{ "v|verbose",
				  "Output information about progress during the run of the tool",
				  v => Verbose = true },
			};

			var remaining = options.Parse (args);

			if (help || args.Length < 1) {
				options.WriteOptionDescriptions (Out);

				Environment.Exit (0);
			}

			if (remaining.Count != 1) {
				Error ("Please specify one <methods-file> to process.");
				Environment.Exit (2);
			}

			return remaining [0];
		}

		static bool TryAddTimeStamp (Dictionary<string, Timestamp> dict, Regex regex, string line, out string method)
		{
			var match = regex.Match (line);

			if (!match.Success || match.Groups.Count <= 2) {
				method = null;
				return false;
			}

			method = match.Groups [1].Value;
			if (dict.ContainsKey (method)) {
				if (Verbose)
					WriteLine ($"Warning: method {method} already measured, dropping the second JIT time");
				return true;
			}

			dict [method] = Timestamp.Parse (match.Groups [2].Value);

			return true;
		}

		public static int Main (string [] args)
		{
			var path = ProcessArguments (args);
			var file = File.OpenText (path);

			var beginRegex = new Regex (@"^JIT method +begin: (.*) elapsed: (.*)$");
			var doneRegex = new Regex (@"^JIT method +done: (.*) elapsed: (.*)$");

			string line;
			int lineNumber = 0;

			var beginTimes = new Dictionary<string, Timestamp> ();
			var doneTimes = new Dictionary<string, Timestamp> ();
			var totalTimes = new Dictionary<string, Timestamp> ();
			var innerMethods = new Dictionary<string, List<string>> ();
			var jitMethods = new Stack<string> ();
			string method;

			while ((line = file.ReadLine ()) != null) {
				lineNumber++;

				if (TryAddTimeStamp (beginTimes, beginRegex, line, out method)) {
					jitMethods.Push (method);
					continue;
				}

				if (TryAddTimeStamp (doneTimes, doneRegex, line, out method)) {
					if (beginTimes.TryGetValue (method, out var begin))
						totalTimes [method] = doneTimes [method] - begin;
					else {
						if (Verbose)
							WriteLine ($"Warning: missing JIT begin for method {method}");
						continue;
					}

					jitMethods.Pop ();

					if (jitMethods.Count > 0) {
						var outerMethod = jitMethods.Peek ();
						List<string> list;
						if (!innerMethods.TryGetValue (outerMethod, out list)) {
							list = new List<string> ();
							innerMethods [outerMethod] = list;
						}
						list.Add (method);
					}
				}
			}

			var selfTimes = new Dictionary<string, Timestamp> ();
			foreach (var pair in totalTimes) {
				var total = pair.Value;
				var self = total;
				if (innerMethods.TryGetValue (pair.Key, out var list)) {
					foreach (var inner in list) {
						if (totalTimes.TryGetValue (inner, out var time))
							self -= time;
					}
				}
				selfTimes [pair.Key] = self;
			}

			ColorWriteLine ("Total (ms) |  Self (ms) | Method", ConsoleColor.Yellow);

			Timestamp sum = new Timestamp ();
			IEnumerable<KeyValuePair<string, Timestamp>> enumerable = null;

			switch (sortKind) {
			case SortKind.Unsorted:
				enumerable = totalTimes;
				break;
			case SortKind.Self:
				enumerable = selfTimes.OrderByDescending (p => p.Value);
				break;
			case SortKind.Total:
				enumerable = totalTimes.OrderByDescending (p => p.Value);
				break;
			}

			foreach (var pair in enumerable) {
				if (methodNameRegexes.Count > 0) {
					var success = false;
					foreach (var filter in methodNameRegexes) {
						var match = filter.Match (pair.Key);
						success |= match.Success;
					}

					if (!success)
						continue;
				}

				var self = selfTimes [pair.Key];
				var total = totalTimes [pair.Key];
				WriteLine ($"{total.Milliseconds (),10:F2} | {self.Milliseconds (),10:F2} | {pair.Key}");

				sum += self;
			}

			ColorWriteLine ($"Sum of self time (ms): {sum.Milliseconds ():F2}", ConsoleColor.Yellow);

			return 0;
		}

		static void ColorMessage (string message, ConsoleColor color, TextWriter writer, bool writeLine = true)
		{
			ForegroundColor = color;

			if (writeLine)
				writer.WriteLine (message);
			else
				writer.Write (message);
			ResetColor ();
		}

		public static void ColorWriteLine (string message, ConsoleColor color) => ColorMessage (message, color, Out);

		public static void ColorWrite (string message, ConsoleColor color) => ColorMessage (message, color, Out, false);

		public static void Error (string message) => ColorMessage ($"Error: {Name}: {message}", ConsoleColor.Red, Console.Error);

		public static void Warning (string message) => ColorMessage ($"Warning: {Name}: {message}", ConsoleColor.Yellow, Console.Error);
	}
}
