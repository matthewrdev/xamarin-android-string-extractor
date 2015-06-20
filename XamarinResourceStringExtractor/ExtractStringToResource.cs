using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.CSharp.TypeSystem;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using Mono.TextEditor;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MonoDroid;
using MonoDevelop.Refactoring;
using MonoDevelop.Refactoring.Rename;

namespace XamarinResourceStringExtractor
{
	public class ExtractStringToResource : RenameRefactoring
	{
		public readonly string[] SupportedResolvers = new string[]  { "Android.App.Activity", "Android.App.Fragment" };

		const string NEW_FILE_CONTENTS = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n{0}\n</resources>\n";
		const string INNER_CONTENT_FORMAT = "\t<string name=\"{0}\">{1}</string>\n";
		const string DEFAULT_STRINGS_FILE_NAME = "strings.xml";
		const string RESOURCE_FUNC_TEMPLATE = "Resources.GetString(Resource.String.{0})";
		
		public ExtractStringToResource ()
		{
			base.Name = "Extract String Resource";
		}

		public override string GetMenuDescription (RefactoringOptions options) 
		{
			return "Extract String Resource";
		}

		public override bool IsValid (RefactoringOptions options)
		{
			var e = options.SelectedItem;
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null || doc.ParsedDocument == null)
				return false;
			
			var ast = doc.ParsedDocument.GetAst<SyntaxTree> ();
			if (ast == null)
				return false;

			var parsedFile = doc.ParsedDocument.ParsedFile as CSharpUnresolvedFile;
			if (parsedFile == null)
				return false;

			var project = doc.Project;
			MonoDroidProject monoDroidProject = project as MonoDroidProject;
			if (monoDroidProject == null)
				return false;

			// Check if the 
			var result = options.ResolveResult;
			if (result == null)
				return false;
			

			var loc = options.Location;
			AstNode node;
			ResolveAtLocation.Resolve (new Lazy<ICompilation>(() => doc.Compilation), parsedFile, ast, loc, out node);
			var owningClass = AstHelper.ResolveNodeEncapsulatingClass (node) as TypeDeclaration;

			if (owningClass == null) {
				return false;
			}

			bool supported = AstHelper.IsClassDerivedFrom (owningClass, SupportedResolvers);

			if (!supported) {
				return false;
			}

			var item = options.SelectedItem;

			bool valid = false;
			ConstantResolveResult c = result as ConstantResolveResult;
			valid = c != null && c.Type.FullName == "System.String";

			return valid;
		}

		int GetContentOffset (string stringsFilePath)
		{
			var text = File.ReadAllText (stringsFilePath);
			return text.LastIndexOf ("</resources>");
		}

		public override List<Change> PerformChanges (RefactoringOptions options, object properties)
		{
			List<Change> changes = new List<Change> ();

			var doc = IdeApp.Workbench.ActiveDocument;
			MonoDroidProject androidProject = doc.Project as MonoDroidProject;
			// Grab the string resource.
			var editor = doc.GetContent<ITextEditorDataProvider> ();
			var data = editor.GetTextEditorData ();

			var constantResult = options.ResolveResult as ConstantResolveResult;
			RenameProperties props = (RenameProperties)properties;

			PrepareStringResourceContent (changes, androidProject, constantResult, props);

			var parsedDocument = doc.ParsedDocument;
			var ast = parsedDocument.GetAst<SyntaxTree> ();
			var parsedFile = parsedDocument.ParsedFile as CSharpUnresolvedFile;

			var loc = options.Location;
			AstNode node;
			var resolveResult = ResolveAtLocation.Resolve (new Lazy<ICompilation>(() => doc.Compilation), parsedFile, ast, loc, out node);

			int startOffset = data.LocationToOffset (node.StartLocation.Line, node.StartLocation.Column);
			int endOffset = data.LocationToOffset (node.EndLocation.Line, node.EndLocation.Column);

			TextReplaceChange replaceChange = new TextReplaceChange ();
			replaceChange.RemovedChars = endOffset - startOffset;
			replaceChange.InsertedText = String.Format (RESOURCE_FUNC_TEMPLATE, props.NewName);
			replaceChange.Description = "Replace " + constantResult.ConstantValue.ToString () + " string literal with resource reference " + props.NewName;
			replaceChange.Offset = data.LocationToOffset (node.StartLocation.Line, node.StartLocation.Column);
			replaceChange.FileName = options.Document.FileName;

			changes.Add (replaceChange);

			return changes;
		}

		void PrepareStringResourceContent (List<Change> changes, MonoDroidProject androidProject, ConstantResolveResult constantResult, RenameProperties props)
		{
			bool needsStringsFile = false;
			string stringsFilePath = "";

			// Verify the strings file. (Resources/value/strings.xml). Create if not there.
			var valuesDirectories = ResourceHelper.ResolveToResourceDirectories (ResourceFolders.VALUES, androidProject);
			string valuesDirectoryPath = valuesDirectories.FirstOrDefault (d => new DirectoryInfo (d).Name == ResourceFolders.VALUES);
			var resgenPath = new FileInfo (androidProject.AndroidResgenFile.FullPath);
			if (valuesDirectoryPath == null)
			{
				needsStringsFile = true;
				valuesDirectoryPath = Path.Combine (resgenPath.Directory.Name, ResourceFolders.VALUES);
				androidProject.AddDirectory (valuesDirectoryPath);
			}
			else
			{
				DirectoryInfo di = new DirectoryInfo (valuesDirectoryPath);
				var valuesFiles = androidProject.GetAndroidResources (di.Name).ToList ();
				stringsFilePath = valuesFiles.FirstOrDefault (v => v.Key.ToLower () == "strings").Value.FilePath.FullPath;
				needsStringsFile = String.IsNullOrEmpty (stringsFilePath);
			}

			var xmlContent = CreateXmlContent (constantResult, props, needsStringsFile);
			if (needsStringsFile)
			{
				stringsFilePath = Path.Combine (valuesDirectoryPath, DEFAULT_STRINGS_FILE_NAME);
				CreateFileChange newFile = new CreateFileChange (stringsFilePath, xmlContent);
				try
				{
					//newFile.PerformChange (null, options);
				}
				catch (Exception ex)
				{
					Console.WriteLine (ex.ToString ());
					;
				}
				changes.Add (newFile);
			}
			else
			{
				TextReplaceChange insertContentChange = new TextReplaceChange ();
				insertContentChange.FileName = stringsFilePath;
				insertContentChange.InsertedText = xmlContent;
				insertContentChange.Description = "Generate Android Resource.String." + props.NewName + " declaration.";
				insertContentChange.Offset = GetContentOffset (stringsFilePath);
				changes.Add (insertContentChange);
			}
		}

		protected string CreateXmlContent(ConstantResolveResult constant, RenameProperties props, bool newFile)
		{
			string content = String.Format(INNER_CONTENT_FORMAT, props.NewName, constant.ConstantValue.ToString());
			if (newFile) {
				content = String.Format (NEW_FILE_CONTENTS, content);
			}
			return content;
		}

		public override void Run (RefactoringOptions options)
		{
			var e = options.SelectedItem;
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null || doc.ParsedDocument == null)
				return;

			var unit = doc.ParsedDocument.GetAst<SyntaxTree> ();
			if (unit == null)
				return;

			var file = doc.ParsedDocument.ParsedFile as CSharpUnresolvedFile;
			if (file == null)
				return;

			var project = doc.Project;
			MonoDroidProject monoDroidProject = project as MonoDroidProject;
			if (monoDroidProject == null)
				return;
			
			// Check if the 
			var result = options.ResolveResult as ConstantResolveResult;
			if (result == null)
				return;
			

			//var gens = CodeGenerationService.CodeGenerators;
			
			var itemDialog = new RenameItemDialog (options, this);
			MessageService.ShowCustomDialog (itemDialog);
		}
	}
}

