// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO;

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class LiveTestConfigurationTests
	{
	private string _directory = null!;
	private string _path = null!;

	[SetUp]
	public void CreatePrivateTestDirectory ()
		{
		_directory = Path.Combine (Path.GetTempPath (), "WiserLiveSettingsTests", Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (_directory);
		_path = Path.Combine (_directory, "LiveTestSettings.json");
		}

	[TearDown]
	public void RemoveSyntheticSettings () => Directory.Delete (_directory, true);

	[Test]
	public void MissingSettings_DefaultToDisabled () =>
		Assert.Throws<IgnoreException> (() => LiveTestSupport.LoadForRun (_path, ""));

	[Test]
	public void ExplicitEnable_RequiresCredentials () =>
		Assert.Throws<InvalidDataException> (() => LiveTestSupport.LoadForRun (_path, "true"));

	[Test]
	public void ExplicitDisable_DoesNotReadMalformedSettings ()
		{
		File.WriteAllText (_path, "invalid json with synthetic-secret");
		Assert.Throws<IgnoreException> (() => LiveTestSupport.LoadForRun (_path, "false"));
		}

	[TestCase ("", true)]
	[TestCase ("true", false)]
	public void LocalFlagOrRunnerOverride_EnablesConfiguredTests (string enableOverride, bool enabledInFile)
		{
		File.WriteAllText (_path, "{\"enabled\":" + enabledInFile.ToString ().ToLowerInvariant () + ",\"hubHost\":\"hub.example\",\"secret\":\"synthetic-secret\"}");
		var settings = LiveTestSupport.LoadForRun (_path, enableOverride);
		Assert.That (settings.HubHost, Is.EqualTo ("hub.example"));
		Assert.That (settings.TimeoutSeconds, Is.EqualTo (60));
		}

	[Test]
	public void DisabledFile_IsSkipped ()
		{
		File.WriteAllText (_path, "{\"enabled\":false}");
		Assert.Throws<IgnoreException> (() => LiveTestSupport.LoadForRun (_path, ""));
		}

	[Test]
	public void MalformedSettings_DoNotExposeInputInErrors ()
		{
		File.WriteAllText (_path, "{\"timeoutSeconds\":\"synthetic-secret\"}");
		var error = Assert.Throws<InvalidDataException> (() => LiveTestSupport.LoadForRun (_path, "true"));
		Assert.That (error!.ToString (), Does.Not.Contain ("synthetic-secret"));
		}

	[TestCase ("yes")]
	[TestCase ("1")]
	public void InvalidEnableOverride_IsRejected (string value) =>
		Assert.Throws<InvalidDataException> (() => LiveTestSupport.ParseOverride (value));

	[TestCase ("", "secret", 60)]
	[TestCase ("hub.example", "", 60)]
	[TestCase ("http://hub.example", "secret", 60)]
	[TestCase ("hub.example/path", "secret", 60)]
	[TestCase ("hub.example", "secret", 0)]
	[TestCase ("hub.example", "secret", 121)]
	public void InvalidConnectionSettings_AreRejected (string host, string secret, int timeout) =>
		Assert.Throws<InvalidDataException> (() => LiveTestSupport.Validate (new LiveTestSettings
			{
			HubHost = host, Secret = secret, TimeoutSeconds = timeout
			}));

	[Test]
	public void RunnerDirectory_IsAuthoritative () =>
		Assert.That (LiveTestSupport.SettingsPath (_directory), Is.EqualTo (_path));
	}