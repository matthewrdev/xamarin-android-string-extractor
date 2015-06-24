using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly:Addin (
	"Xamarin.Android Resource String Extractor", 
	Namespace = "XamarinResourceStringExtractor",
	Version = "0.0.3"
)]

[assembly:AddinName ("Xamarin.Android Resource String Extractor")]
[assembly:AddinCategory ("IDE extensions")]
[assembly:AddinDescription ("A small tool to extract string literals within Activities and Fragments into the strings resource file.\n    \nThis is a sample application for a wider set of refactoring tools for Xamarin.Android. Please submit bug reports to:\nhttps://github.com/matthew-ch-robbins/xamarin-android-string-extractor\n    \nTo help shape the direction of this project, connect with me on LinkedIn and have a chat:\nhttps://au.linkedin.com/pub/matthew-robbins/17/8a7/139")]
[assembly:AddinUrl("https://github.com/matthew-ch-robbins/xamarin-android-string-extractor")]
[assembly:AddinAuthor ("Matthew Robbins")]
