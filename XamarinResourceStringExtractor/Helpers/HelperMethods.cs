// 
// HelperMethods.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2011 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.NRefactory.Completion;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.CSharp.TypeSystem;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using Mono.TextEditor;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects;

namespace XamarinResourceStringExtractor
{
	static class HelperMethods
	{
		public const string MimeType = "text/x-csharp";

		public static void SetText (this CompletionData data, string text)
		{
			if (data is CompletionData) {
				((CompletionData)data).CompletionText = text;
			} else if (data is IEntityCompletionData) {
				((IEntityCompletionData)data).CompletionText = text;
			} else {
				System.Console.WriteLine("Unknown completion data:" + data);
			}
		}

		public static SyntaxTree Parse (this CSharpParser parser, TextEditorData data)
		{
			using (var stream = data.OpenStream ()) {
				return parser.Parse (stream, data.Document.FileName);
			}
		}

		internal static MonoDevelop.CSharp.Formatting.CSharpFormattingPolicy GetFormattingPolicy (this Document doc)
		{
			var policyParent = doc.Project != null ? doc.Project.Policies : null;
			var types = MonoDevelop.Ide.DesktopService.GetMimeTypeInheritanceChain (MimeType);
			var codePolicy = policyParent != null ? policyParent.Get<MonoDevelop.CSharp.Formatting.CSharpFormattingPolicy> (types) : MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<MonoDevelop.CSharp.Formatting.CSharpFormattingPolicy> (types);
			return codePolicy;
		}

		public static CSharpFormattingOptions GetFormattingOptions (this Document doc)
		{
			return GetFormattingPolicy (doc).CreateOptions ();
		}

		public static CSharpFormattingOptions GetFormattingOptions (this Project project)
		{
			var types = MonoDevelop.Ide.DesktopService.GetMimeTypeInheritanceChain (MimeType);
			var codePolicy = project != null ? project.Policies.Get<MonoDevelop.CSharp.Formatting.CSharpFormattingPolicy> (types) :
				MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<MonoDevelop.CSharp.Formatting.CSharpFormattingPolicy> (types);
			return codePolicy.CreateOptions ();
		}

		public static bool TryResolveAt (this Document doc, DocumentLocation loc, out ResolveResult result, out AstNode node)
		{
			if (doc == null)
				throw new ArgumentNullException ("doc");
			result = null;
			node = null;
			var parsedDocument = doc.ParsedDocument;
			if (parsedDocument == null)
				return false;

			var ast = parsedDocument.GetAst<SyntaxTree> ();
			var parsedFile = parsedDocument.ParsedFile as CSharpUnresolvedFile;

			if (ast == null || parsedFile == null)
				return false;
			try {
				result = ResolveAtLocation.Resolve (new Lazy<ICompilation>(() => doc.Compilation), parsedFile, ast, loc, out node);
				if (result == null || node is Statement)
					return false;
			} catch (Exception e) {
				Console.WriteLine ("Got resolver exception:" + e);
				return false;
			}
			return true;
		}
	}
}
