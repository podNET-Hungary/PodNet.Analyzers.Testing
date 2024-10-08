﻿using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PodNet.Analyzers.Testing.CSharp;
using System.Reflection;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CodeActions;
using File = (string Name, string Content);
using Fakes = PodNet.Analyzers.Testing.CodeAnalysis.Fakes;
using System.Collections.Immutable;

namespace PodNet.Analyzers.Testing;

/// <summary>
/// An opinionated but highly configurable test case, which can be used to execute and test incremental generators. When deriving from this class, you should apply <see cref="TestClassAttribute"/> to the class for the test methods to be invoked properly.
/// </summary>
/// <typeparam name="TGenerator">The type of the incremental generator to test.</typeparam>
/// <typeparam name="TEmbeddedTest">The test object. It's shape should be defined as:
/// <code>
/// public static string *Source_cs { get; } // The source to supply to the generator. 
/// public static string *Generated_cs { get; } // The expected generated code.
/// public static string *Script_cs { get; } // The C# script to execute on the generated code. Should return nothing, and throw if the generated code doesn't work as expected. Have access to the parent runtime execution context, including references of the current project.
/// </code>
/// Multiples can be supplied by any of the above, and the number of each sets are unrelated
/// Pro tip: use the <a href="https://www.nuget.org/packages/PodNet.EmbeddedTexts">PodNet.EmbeddedTexts</a> package to automatically generate objects of this shape by setting the following in the .csproj and organizing the Source.cs/Generated.cs/Script.cs/Source.Fixed.cs files in subfolders:
/// <code>
/// &lt;Compile Remove="{YourTestCasesPathHere}\**" /&gt;
/// &lt;AdditionalFiles Include = "{YourTestCasesPathHere}\**" PodNet_EmbedTextDirectoryAsClass="true" /&gt;
/// </code>
/// </typeparam>
/// <remarks>See also <seealso cref="EmbeddedTestCase{TGenerator, TEmbeddedTest}.WithCodeFix{TCodeFix}"/> for an opinionated descendant of this class that also tests <see cref="CodeFixProvider"/>s.</remarks>
public class EmbeddedTestCase<TGenerator, TEmbeddedTest> where TGenerator : IIncrementalGenerator, new()
{
    /// <summary>
    /// Used by <see cref="AssertGenerationRunResultDiagnostics(GeneratorDriverRunResult)"/> to assert the generated diagnostics match the expected diagnostics.
    /// </summary>
    public virtual List<DiagnosticDescriptor>? ExpectedDescriptorsForGenerator { get; }
    /// <summary>
    /// Used by <see cref="AssertGenerationResults(GeneratorDriverRunResult, CSharpCompilation)"/> to ignore checking the generated sources. Useful when using the generated results for execution or scripting tests only.
    /// </summary>
    public virtual bool IgnoreSources { get; }

    /// <summary>
    /// The generator(s) to test. Defaults to an array containing a single <typeparamref name="TGenerator"/>.
    /// </summary>
    public virtual IIncrementalGenerator[] Generators { get; } = [new TGenerator()];

    /// <summary>
    /// The public static properties of <typeparamref name="TEmbeddedTest"/>.
    /// </summary>
    protected static IEnumerable<PropertyInfo> StaticProperties { get; } = typeof(TEmbeddedTest).GetProperties(BindingFlags.Public | BindingFlags.Static);

    /// <summary>
    /// The property key-values (values typed as strings) of the provided properties. Can replace the underscores in names with dots to simulate filenames instead of properties.
    /// </summary>
    /// <param name="properties">The static properties to transform.</param>
    /// <param name="replaceUnderscoreWithDot">Set to true to replace the '_' characters with '.' characters in the property names (not the content).</param>
    /// <returns>The transformed properties.</returns>
    protected IReadOnlyCollection<File> GetPropertyKeyValues(IEnumerable<PropertyInfo> properties, bool replaceUnderscoreWithDot) => properties.Select(p => (replaceUnderscoreWithDot ? p.Name.Replace('_', '.') : p.Name, p.GetValue(null)!.ToString()!)).ToList().AsReadOnly();

    /// <summary>
    /// Gets the static property name-values of <typeparamref name="TEmbeddedTest"/> with the given <paramref name="nameSuffix"/>.
    /// </summary>
    /// <param name="nameSuffix">The suffix to search for on the object's static properties.</param>
    /// <param name="replaceUnderscoreWithDot">Set to true (default) to replace the underscores in the Name value (not the Content) with dots. This mimics filenames with extensions better, although recreating the original filenames is not possible.</param>
    /// <returns>The property name-values of <typeparamref name="TEmbeddedTest"/> with the suffix <paramref name="nameSuffix"/>.</returns>
    public virtual IReadOnlyCollection<File> GetStaticPropertyValues(string nameSuffix, bool replaceUnderscoreWithDot = true) => GetPropertyKeyValues(StaticProperties.Where(p => p.Name.EndsWith(nameSuffix)), replaceUnderscoreWithDot);

    /// <summary>
    /// Gets the static property name-values of <typeparamref name="TEmbeddedTest"/> with the suffix "Source_cs", with the name's underscores replaced with dots to mimic filenames.
    /// </summary>
    /// <returns>The property name-values.</returns>
    public virtual IReadOnlyCollection<File> GetSources() => GetStaticPropertyValues("Source_cs");

    /// <summary>
    /// Gets the static property name-values of <typeparamref name="TEmbeddedTest"/> which do NOT have the suffix "_cs", with the name's underscores replaced with dots to mimic filenames.
    /// </summary>
    /// <returns>The property name-values.</returns>
    public virtual IReadOnlyCollection<File> GetAdditionalTexts() => GetPropertyKeyValues(StaticProperties.Where(p => !p.Name.EndsWith("_cs")), true);

    /// <summary>
    /// Gets the static property name-values of <typeparamref name="TEmbeddedTest"/> with the suffix "Generated_cs", with the name's underscores replaced with dots to mimic filenames.
    /// </summary>
    /// <returns>The property name-values.</returns>
    public virtual IReadOnlyCollection<File> GetExpectedGeneratedSources() => GetStaticPropertyValues("Generated_cs");

    /// <summary>
    /// Gets the static property name-values of <typeparamref name="TEmbeddedTest"/> with the suffix "Script_cs", with the name's underscores replaced with dots to mimic filenames.
    /// </summary>
    /// <returns>The property name-values.</returns>
    public virtual IReadOnlyCollection<File> GetScripts() => GetStaticPropertyValues("Script_cs");

    /// <summary>
    /// The test method. Gets all sources and additional texts, runs the generator, then asserts the generator results and behavior (scripts) are correct.
    /// </summary>
    /// <returns></returns>
    [TestMethod]
    public virtual async Task GeneratedCodeMatchesAndWorksAsExpectedAsync()
    {
        var sources = GetSources();
        var additionalTexts = GetAdditionalTexts();
        var result = RunGenerator(sources, additionalTexts, out var outputCompilation);
        AssertGenerationResults(result, outputCompilation);
        await AssertGeneratedBehaviorAsync(outputCompilation);
    }

    /// <summary>
    /// Creates the <see cref="CSharpCompilation"/> for the given <paramref name="syntaxTrees"/>. Uses <see cref="PodCSharpCompilation"/> by default.
    /// </summary>
    /// <param name="syntaxTrees">The sources (filename-content pairs) to include in the compilation. Note that the file names don't include the namespace/path of the original files by default.</param>
    /// <param name="additionalFiles">The additional text files to be supplied to the generator. Not used by default.</param>
    /// <returns>The compilation.</returns>
    public virtual CSharpCompilation CreateCompilation(ImmutableArray<SyntaxTree> syntaxTrees, ImmutableArray<AdditionalText> additionalFiles) => PodCSharpCompilation.Create(syntaxTrees);

    /// <summary>
    /// Creates the <see cref="CSharpCompilation"/> to execute <see cref="Generators"/> on, executes the generators and returns the results.
    /// </summary>
    /// <param name="sources">The sources to create the <see cref="CSharpCompilation"/> with.</param>
    /// <param name="additionalTexts">The additional text files to provide to generator.</param>
    /// <param name="outputCompilation">The compilation produced by running the compilation.</param>
    /// <returns>The result of the generator run.</returns>
    public virtual GeneratorDriverRunResult RunGenerator(IEnumerable<File> sources, IEnumerable<File> additionalTexts, out CSharpCompilation outputCompilation)
    {
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s.Content, path: s.Name)).ToImmutableArray();
        var additionalFiles = additionalTexts.Select(a => new Fakes.AdditionalText(a.Name, a.Content)).ToImmutableArray<AdditionalText>();
        var compilation = CreateCompilation(syntaxTrees, additionalFiles);
        return compilation.RunGenerators(Generators, out _, out outputCompilation, d => ConfigureGeneratorDriver(d, syntaxTrees, additionalFiles));
    }

    /// <summary>
    /// Configures the generator driver for running the generators on. By default, adds the provided <paramref name="additionalTexts"/> to the driver as <see cref="Fakes.AdditionalText"/>s.
    /// </summary>
    /// <param name="driver">The driver instance to configure. Immutable, but can be copied (or disregarded).</param>
    /// <param name="syntaxTrees">The syntax trees. Can be used to define the dictionary for the <see cref="Fakes.AnalyzerConfigOptionsProvider"/>.</param>
    /// <param name="additionalTexts">The additional texts to populate the driver with. Can also be used to define the dictionary for the <see cref="Fakes.AnalyzerConfigOptionsProvider"/>.</param>
    /// <returns>The configured driver.</returns>
    public virtual CSharpGeneratorDriver ConfigureGeneratorDriver(CSharpGeneratorDriver driver, ImmutableArray<SyntaxTree> syntaxTrees, ImmutableArray<AdditionalText> additionalTexts)
        => (CSharpGeneratorDriver)driver
            .AddAdditionalTexts(additionalTexts)
            .WithUpdatedAnalyzerConfigOptions(new Fakes.AnalyzerConfigOptionsProvider(GlobalAnalyzerConfigOptions, syntaxTrees.ToDictionary(s => s, GetOptionsForSyntaxTree), additionalTexts.ToDictionary(s => s, GetOptionsForAdditionalText)));

    /// <summary>
    /// Gets the global options to supply to the generator. By default, adds the name of <typeparamref name="TEmbeddedTest"/> as the <c>"build_property.rootnamespace"</c> to the options. Use <see cref="Fakes.AnalyzerConfigOptions"/> to supply your own.
    /// </summary>
    /// <returns>The configured options.</returns>
    public virtual Fakes.AnalyzerConfigOptions GlobalAnalyzerConfigOptions { get; } = new()
    {
        ["build_property.rootnamespace"] = typeof(TEmbeddedTest).Name
    };

    /// <summary>
    /// An options instance containing no values.
    /// </summary>
    protected static Fakes.AnalyzerConfigOptions EmptyAnalyzerOptions { get; } = [];

    /// <summary>
    /// Used to configure the options provided for each <see cref="SyntaxTree"/> to the generator. Empty by default.
    /// </summary>
    /// <param name="syntaxTree">The tree to provide the options for.</param>
    /// <returns>The options for the given tree.</returns>
    public virtual Fakes.AnalyzerConfigOptions GetOptionsForSyntaxTree(SyntaxTree syntaxTree) => EmptyAnalyzerOptions;

    /// <summary>
    /// Used to configure the options provided for each <see cref="AdditionalText"/> to the generator. Empty by default.
    /// </summary>
    /// <param name="additionalText">The file to provide the options for.</param>
    /// <returns>The options for the given text file.</returns>
    public virtual Fakes.AnalyzerConfigOptions GetOptionsForAdditionalText(AdditionalText additionalText) => EmptyAnalyzerOptions;

    /// <summary>
    /// Asserts that the results are of correct shape: the result has the expected diagnostics and the generated trees are structurally equivalent to the expected trees (see <see cref="GetExpectedGeneratedSources"/>).
    /// </summary>
    /// <param name="result">The result of the generator run.</param>
    /// <param name="outputCompilation">The resulting compilation of the generator run.</param>
    public virtual void AssertGenerationResults(GeneratorDriverRunResult result, CSharpCompilation outputCompilation)
    {
        AssertDiagnostics(result, outputCompilation);

        if (IgnoreSources)
            return;

        var expectedSources = GetExpectedGeneratedSources();
        Assert.AreEqual(result.GeneratedTrees.Length, expectedSources.Count, $"Expected {expectedSources.Count} generated sources, actual: {result.GeneratedTrees.Length}");

        foreach (var (name, expectedSource) in expectedSources)
        {
            var expectedSyntax = CSharpSyntaxTree.ParseText(expectedSource);
            if (result.GeneratedTrees.Length == 1)
            {
                // We already checked for both to contain a single one, so expectedSource is the single that should match the single generated tree here
                var areEquivalent = SyntaxFactory.AreEquivalent(expectedSyntax, result.GeneratedTrees[0], false);
                // We don't use Assert.IsTrue here so we don't have to do costly ToString and diff on the generated tree for the message unless the syntaxes are different
                if (!areEquivalent)
                    Assert.Fail($"The generated tree isn't syntactically equivalent to the expected source {name}.\r\n{Diffing.TextDiff.InlineDiff(expectedSource, result.GeneratedTrees[0].ToString(), true)}");
            }
            else
            {
                // Find the matching one, if any
                Assert.IsTrue(result.GeneratedTrees.Any(g => SyntaxFactory.AreEquivalent(expectedSyntax, g, false)), $"None of the {result.GeneratedTrees.Length} generated trees matched the expected syntax of '{typeof(TEmbeddedTest).Name}.{name}'.");
            }
        }
    }

    /// <summary>
    /// Asserts the diagnostics in the generator run result and compilation.
    /// </summary>
    /// <param name="result">The result of the generator run.</param>
    /// <param name="outputCompilation">The resulting compilation of the generator run.</param>
    public virtual void AssertDiagnostics(GeneratorDriverRunResult result, CSharpCompilation outputCompilation)
    {
        AssertGenerationRunResultDiagnostics(result);
        AssertCompilationDiagnostics(outputCompilation);
    }

    /// <summary>
    /// Asserts that the generators produced exactly the diagnostics as set by <see cref="ExpectedDescriptorsForGenerator"/>.
    /// </summary>
    /// <param name="result">The result of the generator run to assert.</param>
    public virtual void AssertGenerationRunResultDiagnostics(GeneratorDriverRunResult result)
    {
        if (ExpectedDescriptorsForGenerator is { Count: > 0 })
            CollectionAssert.AreEquivalent(ExpectedDescriptorsForGenerator, result.Diagnostics.Select(d => d.Descriptor).ToList());
        else
            AssertNoDiagnostics(result.Diagnostics, "Expected no diagnostics from the generator.");

    }

    /// <summary>
    /// Assert that there are no <see cref="DiagnosticSeverity.Warning"/>s or <see cref="DiagnosticSeverity.Error"/>s in the resulting compilation.
    /// </summary>
    /// <param name="compilation">To compilation after the generators have ran.</param>
    public virtual void AssertCompilationDiagnostics(CSharpCompilation compilation)
    {
        AssertNoDiagnostics(compilation.GetDiagnostics().Where(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error), "Expected no diagnostics of Warning or Error from the resulting compilation.");
    }

    /// <summary>
    /// Asserts the provided diagnostics are empty.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to assert are empty.</param>
    /// <param name="message">The message shown when the assert fails. Prepended to the diagnostics printed.</param>
    protected static void AssertNoDiagnostics(IEnumerable<Diagnostic> diagnostics, string message)
    {
        Assert.AreEqual(0, diagnostics.TryGetNonEnumeratedCount(out var count) ? count : diagnostics.Count(), $"{message}\r\n{string.Join("\r\n", diagnostics.Select((d, i) => $"[{i}, {d.Location}] {d.Severity} {d.Id} {d.GetMessage()}"))}");
    }

    /// <summary>
    /// Configures the script options to execute the scripts. By default, adds references to "Microsoft.VisualStudio.TestPlatform.TestFramework" and "netstandard", and adds an import to "Microsoft.VisualStudio.TestTools.UnitTesting".
    /// </summary>
    /// <param name="options">The options to configure. You can chain calls to this to modify or return a different instance; the <see cref="ScriptOptions"/> are immutable.</param>
    /// <returns>The options configured.</returns>
    public virtual ScriptOptions ConfigureScriptOptions(ScriptOptions options) => options
        .AddReferences("Microsoft.VisualStudio.TestPlatform.TestFramework", "netstandard")
        .AddImports("Microsoft.VisualStudio.TestTools.UnitTesting");

    /// <summary>
    /// Asserts all scripts (returned by <see cref="GetScripts"/> execute correctly and do not throw.
    /// </summary>
    /// <param name="outputCompilation">The compilation to run the scripts on. This is the result of the generator run.</param>
    /// <returns>The task representing the async operation.</returns>
    public virtual async Task AssertGeneratedBehaviorAsync(CSharpCompilation outputCompilation)
    {
        var scripts = GetScripts();
        foreach (var (name, script) in scripts)
        {
            var scriptRunResult = await outputCompilation.ExecuteScriptAsync<object?>(script, configureOptions: ConfigureScriptOptions);
            Assert.IsTrue(scriptRunResult.EmitResult.Success, $"Expected script '{typeof(TEmbeddedTest).Name}.{name}' to compile and emit correctly.");
            Assert.IsNull(scriptRunResult.ScriptResult, $"Expected script '{typeof(TEmbeddedTest).Name}.{name}' to return null.");
        }
    }

    /// <summary>
    /// An opinionated descendant of <see cref="EmbeddedTestCase{TGenerator, TEmbeddedTest}"/>, which expands its capabilities with checking for code fixes.
    /// </summary>
    /// <typeparam name="TCodeFix"></typeparam>
    public class WithCodeFix<TCodeFix> : EmbeddedTestCase<TGenerator, TEmbeddedTest>
        where TCodeFix : CodeFixProvider, new()
    {
        /// <summary>
        /// Gets the static property name-values of <typeparamref name="TEmbeddedTest"/> with the suffix "Source_Fixed_cs", with the name's underscores replaced with dots to mimic filenames. These will be the expected results of the diagnostics produced for the current test case by <typeparamref name="TGenerator"/>.
        /// </summary>
        /// <returns>The property name-values.</returns>
        public virtual IReadOnlyCollection<File> GetFixedSources() => GetStaticPropertyValues("Source_Fixed_cs");

        /// <summary>
        /// Tests for the code fix to work as expected. The <see cref="GetSources"/> should produce diagnostics (<see cref="ExpectedDescriptorsForGenerator"/>) that are fixed by <typeparamref name="TGenerator"/>. The results of the fixes have to be defined by <see cref="GetFixedSources"/>.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public virtual async Task CodeFixWorksAsExpectedAsync()
        {
            List<string> sourcesWithAppliedFixes = [];

            var codeFix = new TCodeFix();
            var result = RunGenerator(GetSources(), GetAdditionalTexts(), out var compilation);

            var project = new AdhocWorkspace().AddProject(codeFix.GetType().Name, LanguageNames.CSharp);
            foreach (var tree in compilation.SyntaxTrees)
                project = project.AddDocument(Path.GetFileName(tree.FilePath), tree.GetText(), filePath: tree.FilePath).Project;

            foreach (var diagnosticToFix in result.Diagnostics.Where(d => codeFix.FixableDiagnosticIds.Contains(d.Id)))
            {
                if (diagnosticToFix.Location.SourceTree is null)
                    throw new AssertFailedException("The required source tree was not available for extraction in the produced Diagnostic.");
                var sourceText = diagnosticToFix.Location.SourceTree.GetText().ToString();
                foreach (var document in project.Documents)
                {
                    var source = await document.GetTextAsync();
                    if (string.Equals(source.ToString(), sourceText))
                    {
                        var context = new CodeFixContext(document, diagnosticToFix, (codeAction, diagnostics) =>
                        {
                            var operations = codeAction.GetOperationsAsync(CancellationToken.None).GetAwaiter().GetResult();
                            foreach (var operation in operations)
                            {
                                if (operation is ApplyChangesOperation acOperation)
                                {
                                    var newDocument = acOperation.ChangedSolution.GetDocument(document.Id);
                                    // We find the updated document by the original's id
                                    Assert.IsNotNull(newDocument);
                                    // Collect the updated document's source content for assertion later on
                                    sourcesWithAppliedFixes.Add(newDocument!.GetTextAsync().GetAwaiter().GetResult().ToString());
                                }
                                else
                                {
                                    // Should be no need to call operation.Apply here
                                    Assert.Fail("The operation recieved from the code fix wasn't of the expected type");
                                }
                            }
                        }, default);
                        await codeFix.RegisterCodeFixesAsync(context);
                        break;
                    }
                }
            }

            var expectedFixes = GetFixedSources();
            Assert.AreEqual(expectedFixes.Count, sourcesWithAppliedFixes.Count, $"The number of applied fixes ({sourcesWithAppliedFixes.Count}) didn't match the number of expected fixes ({expectedFixes.Count})");
            CollectionAssert.AreEquivalent(expectedFixes.Select(s => s.Content).Order().ToList(), sourcesWithAppliedFixes.Order().ToList(), "The applied code fixes didn't match the expected sources");
        }
    }
}
