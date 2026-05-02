// Amplify Shader Editor - Visual Shader Editing Tool
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmplifyShaderEditor
{
	public enum ASEImportFlags
	{
		None = 0,
		URP  = 1 << 0,
		HDRP = 1 << 1,
		Both = URP | HDRP
	}

	public static class AssetDatabaseEX
	{
		private static System.Type type = null;
		public static System.Type Type { get { return ( type == null ) ? type = System.Type.GetType( "UnityEditor.AssetDatabase, UnityEditor" ) : type; } }

		public static void ImportPackageImmediately( string packagePath )
		{
			AssetDatabaseEX.Type.InvokeMember( "ImportPackageImmediately", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, null, new object[] { packagePath } );
		}
	}

	public enum SRPBaseline
	{
		ASE_SRP_INVALID = 0,
		ASE_SRP_14_X = 140000,
		ASE_SRP_15_X = 150000,
		ASE_SRP_16_X = 160000,
		ASE_SRP_17_0 = 170000,
		ASE_SRP_17_1 = 170100,
		ASE_SRP_17_2 = 170200,
		ASE_SRP_17_3 = 170300,
		ASE_SRP_17_4 = 170400,
		ASE_SRP_17_5 = 170500
	}

	public class SRPTemplateVersionDesc
	{
		public SRPBaseline baseline = SRPBaseline.ASE_SRP_INVALID;
		public string folderURP = string.Empty;
		public string folderHDRP = string.Empty;

		public SRPTemplateVersionDesc( SRPBaseline baseline, string folderURP, string folderHDRP )
		{
			this.baseline = baseline;
			this.folderURP = folderURP;
			this.folderHDRP = folderHDRP;
		}
	}

	[Serializable]
	[InitializeOnLoad]
	public static class ASEPackageManagerHelper
	{
		public static readonly string URPPackageId  = "com.unity.render-pipelines.universal";
		public static readonly string HDRPPackageId = "com.unity.render-pipelines.high-definition";

		private static readonly string NewVersionDetectedFormat = "[AmplifyShaderEditor] A new {0} version {1} was detected and new templates are being installed.\n";
		private static readonly string DefaultedTemplatesFormat = "[AmplifyShaderEditor] No {0} version was detected and templates were disabled.\n";

		private static readonly string SRPKeywordFormat = "ASE_SRP_VERSION {0}";
		private static readonly string ASEVersionKeywordFormat = "ASE_VERSION {0}";

		public static readonly Dictionary<int, SRPTemplateVersionDesc> SRPPackageSupport = new Dictionary<int,SRPTemplateVersionDesc>()
		{
			{ ( int )SRPBaseline.ASE_SRP_14_X, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_14_X, "", "" ) },
			{ ( int )SRPBaseline.ASE_SRP_15_X, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_15_X, "", "" ) },
			{ ( int )SRPBaseline.ASE_SRP_16_X, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_16_X, "", "" ) },
			{ ( int )SRPBaseline.ASE_SRP_17_0, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_17_0, "", "" ) },
			{ ( int )SRPBaseline.ASE_SRP_17_1, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_17_1, "", "" ) },
			{ ( int )SRPBaseline.ASE_SRP_17_2, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_17_2, "", "" ) },
			{ ( int )SRPBaseline.ASE_SRP_17_3, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_17_3, "", "" ) },
			{ ( int )SRPBaseline.ASE_SRP_17_4, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_17_4, "", "" ) },
			{ ( int )SRPBaseline.ASE_SRP_17_5, new SRPTemplateVersionDesc( SRPBaseline.ASE_SRP_17_5, "", "" ) },
		};

		private static Shader m_lateShader;
		private static Material m_lateMaterial;
		private static AmplifyShaderFunction m_lateShaderFunction;

		private static ListRequest m_packageListRequest = null;
		private static UnityEditor.PackageManager.PackageInfo m_urpPackageInfo;
		private static UnityEditor.PackageManager.PackageInfo m_hdrpPackageInfo;

		public static bool FoundURPVersion { get { return m_urpPackageInfo != null; } }
		public static bool FoundHDRPVersion { get { return m_hdrpPackageInfo != null; } }

		private static bool m_lateImport = false;
		private static string m_latePackageToImport;
		private static bool m_requireUpdateList = false;
		private static ASEImportFlags m_importingPackage = ASEImportFlags.None;

		public static bool CheckImporter { get { return m_importingPackage != ASEImportFlags.None; } }
		public static bool IsProcessing { get { return m_requireUpdateList && m_importingPackage == ASEImportFlags.None; } }

		private static SRPBaseline m_currentURPBaseline = SRPBaseline.ASE_SRP_INVALID;
		private static SRPBaseline m_currentHDRPBaseline = SRPBaseline.ASE_SRP_INVALID;

		public static SRPBaseline CurrentURPBaseline { get { return m_currentURPBaseline; } }
		public static SRPBaseline CurrentHDRPBaseline { get { return m_currentHDRPBaseline; } }

		private static int m_packageURPVersion = -1; // @diogo: starts as missing
		private static int m_packageHDRPVersion = -1;

		public const int MinimumSupportedSRPVersion = ( int )SRPBaseline.ASE_SRP_14_X;

		public static int PackageSRPVersion { get { return ( m_packageHDRPVersion >= m_packageURPVersion ) ? m_packageHDRPVersion : m_packageURPVersion; } }
		public static int CurrentSRPVersion { get { return UIUtils.CurrentWindow.MainGraphInstance.IsSRP ? PackageSRPVersion : -1; } }

		private static string m_projectName = null;
		private static string ProjectName
		{
			get
			{
				if ( string.IsNullOrEmpty( m_projectName ) )
				{
					string[] s = Application.dataPath.Split( '/' );
					m_projectName = s[ s.Length - 2 ];
				}
				return m_projectName;
			}
		}

		public static void Initialize()
		{
			if ( !RequestInfoNow() )
			{
				RequestInfo( true );
			}
		}

		static void WaitForPackageListBeforeUpdating()
		{
			if ( !Application.isPlaying && m_packageListRequest.IsCompleted && m_packageListRequest.Status == StatusCode.Success )
			{
				Update();
				EditorApplication.update -= WaitForPackageListBeforeUpdating;
			}
		}

		public static void RequestInfo( bool updateWhileWaiting = false )
		{
			if ( !Application.isPlaying && !m_requireUpdateList && m_importingPackage == ASEImportFlags.None )
			{
				m_requireUpdateList = true;
				m_packageListRequest = UnityEditor.PackageManager.Client.List( true );
				if ( updateWhileWaiting )
				{
					EditorApplication.update += WaitForPackageListBeforeUpdating;
				}
			}
		}

		public static bool RequestInfoNow()
		{
			UnityEditor.PackageManager.PackageInfo[] packages = null;

			packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
			if ( packages.Length > 0 )
			{
				UpdateNow( packages );
			}

			return packages.Length > 0;
		}

		static void FailedPackageImport( string packageName, string errorMessage )
		{
			FinishImporter();
		}

		static void CancelledPackageImport( string packageName )
		{
			FinishImporter();
		}

		static void CompletedPackageImport( string packageName )
		{
			FinishImporter();
		}

		public static void CheckLatePackageImport()
		{
			if ( !Application.isPlaying && m_lateImport && !string.IsNullOrEmpty( m_latePackageToImport ) )
			{
				m_lateImport = false;
				StartImporting( m_latePackageToImport );
				m_latePackageToImport = string.Empty;
			}
		}

		public static bool StartImporting( string packagePath )
		{
			if ( !Preferences.Project.AutoSRP )
			{
				m_importingPackage = ASEImportFlags.None;
				return false;
			}

			if ( Application.isPlaying )
			{
				if ( !m_lateImport )
				{
					m_lateImport = true;
					m_latePackageToImport = packagePath;
					Debug.LogWarning( "Amplify Shader Editor requires the \"" + packagePath + "\" package to be installed in order to continue. Please exit Play mode to proceed." );
				}
				return false;
			}

			AssetDatabase.importPackageCancelled += CancelledPackageImport;
			AssetDatabase.importPackageCompleted += CompletedPackageImport;
			AssetDatabase.importPackageFailed += FailedPackageImport;
			AssetDatabase.ImportPackage( packagePath, false );
			return true;
		}

		public static void FinishImporter()
		{
			m_importingPackage = ASEImportFlags.None;
			AssetDatabase.importPackageCancelled -= CancelledPackageImport;
			AssetDatabase.importPackageCompleted -= CompletedPackageImport;
			AssetDatabase.importPackageFailed -= FailedPackageImport;
		}

		public static void SetupLateShader( Shader shader )
		{
			if ( shader == null )
				return;

			//If a previous delayed object is pending discard it and register the new one
			// So the last selection will be the choice of opening
			//This can happen when trying to open an ASE canvas while importing templates or in play mode
			if ( m_lateShader != null )
			{
				EditorApplication.delayCall -= LateShaderOpener;
			}

			RequestInfo();
			m_lateShader = shader;
			EditorApplication.delayCall += LateShaderOpener;
		}

		public static void LateShaderOpener()
		{
			Update();
			if ( IsProcessing )
			{
				EditorApplication.delayCall += LateShaderOpener;
			}
			else
			{
				AmplifyShaderEditorWindow.ConvertShaderToASE( m_lateShader );
				m_lateShader = null;
			}
		}

		public static void SetupLateMaterial( Material material )
		{
			if ( material == null )
				return;

			//If a previous delayed object is pending discard it and register the new one
			// So the last selection will be the choice of opening
			//This can happen when trying to open an ASE canvas while importing templates or in play mode
			if ( m_lateMaterial != null )
			{
				EditorApplication.delayCall -= LateMaterialOpener;
			}

			RequestInfo();
			m_lateMaterial = material;
			EditorApplication.delayCall += LateMaterialOpener;
		}

		public static void LateMaterialOpener()
		{
			Update();
			if ( IsProcessing )
			{
				EditorApplication.delayCall += LateMaterialOpener;
			}
			else
			{
				AmplifyShaderEditorWindow.LoadMaterialToASE( m_lateMaterial );
				m_lateMaterial = null;
			}
		}

		public static void SetupLateShaderFunction( AmplifyShaderFunction shaderFunction )
		{
			if ( shaderFunction == null )
				return;

			//If a previous delayed object is pending discard it and register the new one
			// So the last selection will be the choice of opening
			//This can happen when trying to open an ASE canvas while importing templates or in play mode
			if ( m_lateShaderFunction != null )
			{
				EditorApplication.delayCall -= LateShaderFunctionOpener;
			}

			RequestInfo();
			m_lateShaderFunction = shaderFunction;
			EditorApplication.delayCall += LateShaderFunctionOpener;
		}

		public static void LateShaderFunctionOpener()
		{
			Update();
			if ( IsProcessing )
			{
				EditorApplication.delayCall += LateShaderFunctionOpener;
			}
			else
			{
				AmplifyShaderEditorWindow.LoadShaderFunctionToASE( m_lateShaderFunction, false );
				m_lateShaderFunction = null;
			}
		}

		private static readonly string SemVerPattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";

		public static int PackageVersionStringToCode( string version, out int major, out int minor, out int patch )
		{
			MatchCollection matches = Regex.Matches( version, SemVerPattern, RegexOptions.Multiline );

			bool validMatch = ( matches.Count > 0 && matches[ 0 ].Groups.Count >= 4 );
			major = validMatch ? int.Parse( matches[ 0 ].Groups[ 1 ].Value ) : 99;
			minor = validMatch ? int.Parse( matches[ 0 ].Groups[ 2 ].Value ) : 99;
			patch = validMatch ? int.Parse( matches[ 0 ].Groups[ 3 ].Value ) : 99;

			int versionCode;
			versionCode = major * 10000;
			versionCode += minor * 100;
			versionCode += patch;
			return versionCode;
		}

		public static void CodeToPackageVersionElements( int versionCode, out int major, out int minor, out int patch )
		{
			major = versionCode / 10000;
			minor = versionCode / 100 - major * 100;
			patch = versionCode - ( versionCode / 100 ) * 100;
		}

		public static int PackageVersionElementsToCode( int major, int minor, int patch )
		{
			return major * 10000 + minor * 100 + patch;
		}

		public static void CheckActivePackages( UnityEditor.PackageManager.PackageInfo[] packages, ConcurrentBag<string> updatedAssets )
		{
			Debug.Assert( packages.Length > 0 );

			foreach ( UnityEditor.PackageManager.PackageInfo package in packages )
			{
				int versionCode = PackageVersionStringToCode( package.version, out int major, out int minor, out int patch );

				int baselineMajor = major;
				int baselineMinor = ( major >= 17 ) ? minor : 0; // from 17+ baseline includes minor version
				int baseline = PackageVersionElementsToCode( baselineMajor, baselineMinor, 0 );

				SRPTemplateVersionDesc match;
				if ( SRPPackageSupport.TryGetValue( baseline, out match ) )
				{
					if ( package.name.Equals( URPPackageId ) )
					{
						// Universal Rendering Pipeline
						m_currentURPBaseline = match.baseline;
						m_packageURPVersion = versionCode;
						m_urpPackageInfo = package;
					}
					else if ( package.name.Equals( HDRPPackageId ) )
					{
						// High-Definition Rendering Pipeline
						m_currentHDRPBaseline = match.baseline;
						m_packageHDRPVersion = versionCode;
						m_hdrpPackageInfo = package;
					}
				}

				var templateVersionMatch = new List<(string, string)>();

				if ( TemplateTracker.KnownManifests.TryGetValue( package.name, out HashSet<TemplateManifest> manifestSet ) )
				{
					bool foundNewVersion = false;

					foreach ( var manifest in manifestSet )
					{
						foreach ( var template in manifest.Templates )
						{
							foreach ( var version in template.Versions )
							{
								if ( package.version.StartsWith( version.VersionRange.Min ) && !package.version.StartsWith( version.VersionRange.Max ) )
								{
									string templatePath = AssetDatabase.GetAssetPath( template.Template );
									string overridePath = AssetDatabase.GetAssetPath( version.Override );

									templateVersionMatch.Add( (templatePath, overridePath) );
								}
							}
						}
					}

					Parallel.For( 0, templateVersionMatch.Count, i =>
					{
						string templatePath = templateVersionMatch[ i ].Item1;
						string overridePath = templateVersionMatch[ i ].Item2;

						byte[] templateData = File.ReadAllBytes( templatePath );
						byte[] overrideData = File.ReadAllBytes( overridePath );

						uint templateCRC = IOUtils.CRC32( templateData );
						uint overrideCRC = IOUtils.CRC32( overrideData );

						if ( templateCRC != overrideCRC )
						{
							File.WriteAllBytes( templatePath, overrideData );
							updatedAssets.Add( templatePath );
							foundNewVersion = true;
						}
					} );

					if ( foundNewVersion )
					{
						string name = package.name;
						if ( package.name.Equals( URPPackageId ) )
						{
							name = ASEImportFlags.URP.ToString();
						}
						else if ( package.name.Equals( HDRPPackageId ) )
						{
							name = ASEImportFlags.HDRP.ToString();
						}
						Debug.LogFormat( NewVersionDetectedFormat, name, package.version );
					}
				}
			}
		}

		public static void CheckInactivePackages( UnityEditor.PackageManager.PackageInfo[] packages, ConcurrentBag<string> updatedAssets )
		{
			Debug.Assert( packages.Length > 0 );

			var packageNames = packages.Select( p => p.name ).ToHashSet();
			var templateMatch = new List<(string, string, string)>();
			foreach ( var pair in TemplateTracker.KnownManifests )
			{
				if ( !packageNames.Contains( pair.Key ) )
				{
					// Package not found: reset all templates for manifests referencing this package
					foreach ( var manifest in pair.Value )
					{
						foreach ( var template in manifest.Templates )
						{
							string path = AssetDatabase.GetAssetPath( template.Template );
							if ( !string.IsNullOrEmpty( path ) )
							{
								string defaultBody = template.DefaultShaderBody();
								templateMatch.Add( (path, defaultBody, pair.Key) );
							}
						}
					}
				}
			}

			var inactivePackages = new ConcurrentDictionary<string,int>();
			Parallel.For( 0, templateMatch.Count, i =>
			{
				string templatePath = templateMatch[ i ].Item1;

				byte[] templateData = File.ReadAllBytes( templatePath );
				byte[] defaultData = System.Text.Encoding.UTF8.GetBytes( templateMatch[ i ].Item2 );

				uint templateCRC = IOUtils.CRC32( templateData );
				uint defaultCRC = IOUtils.CRC32( defaultData );

				if ( templateCRC != defaultCRC )
				{
					File.WriteAllBytes( templatePath, defaultData );
					updatedAssets.Add( templatePath );
					inactivePackages[ templateMatch[ i ].Item3 ] = 1;
				}
			} );

			foreach ( var pair in inactivePackages )
			{
				string packageName = pair.Key;
				string name = packageName;
				if ( packageName.Equals( URPPackageId ) )
				{
					name = ASEImportFlags.URP.ToString();
				}
				else if ( packageName.Equals( HDRPPackageId ) )
				{
					name = ASEImportFlags.HDRP.ToString();
				}
				Debug.LogFormat( DefaultedTemplatesFormat, name );
			}
		}

		public static void Update()
		{
			CheckLatePackageImport();

			if ( !Application.isPlaying && m_requireUpdateList && m_importingPackage == ASEImportFlags.None )
			{
				if ( m_packageListRequest != null && m_packageListRequest.IsCompleted && m_packageListRequest.Status == StatusCode.Success && m_packageListRequest.Result != null )
				{
					m_requireUpdateList = false;
					UpdateNow( m_packageListRequest.Result.ToArray() );
				}
			}
		}

		public static void UpdateNow( UnityEditor.PackageManager.PackageInfo[] packages )
		{
			TemplateTracker.ScanTemplateManifests();

			var updatedAssets = new ConcurrentBag<string>();

			CheckActivePackages( packages, updatedAssets );

			CheckInactivePackages( packages, updatedAssets );

			try
			{
				if ( updatedAssets.Count > 0 )
				{
					AssetDatabase.StartAssetEditing();
					foreach ( var templatePath in updatedAssets )
					{
						AssetDatabase.ImportAsset( templatePath, ImportAssetOptions.ForceUpdate );
					}
				}
			}
			finally
			{
				if ( updatedAssets.Count > 0 )
				{
					AssetDatabase.StopAssetEditing();
					AssetDatabase.Refresh( ImportAssetOptions.ForceUpdate );
				}
			}
		}

		public static void SetASEVersionInfoOnDataCollector( ref MasterNodeDataCollector dataCollector )
		{
			if ( m_requireUpdateList )
			{
				Update();
			}

			dataCollector.AddToDirectives( string.Format( ASEVersionKeywordFormat, VersionInfo.FullNumber ), -1, AdditionalLineType.Define );
		}

		public static void SetSRPInfoOnDataCollector( ref MasterNodeDataCollector dataCollector )
		{
			if ( m_requireUpdateList )
			{
				Update();
			}

			if ( dataCollector.CurrentSRPType == TemplateSRPType.HDRP )
			{
				dataCollector.AddToDirectives( string.Format( SRPKeywordFormat, m_packageHDRPVersion ), -1, AdditionalLineType.Define );
			}
			else if ( dataCollector.CurrentSRPType == TemplateSRPType.URP )
			{
				dataCollector.AddToDirectives( string.Format( SRPKeywordFormat, m_packageURPVersion ), -1, AdditionalLineType.Define );
			}
		}
	}
}
