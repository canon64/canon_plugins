using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

Console.WriteLine("Embedded UI source audit started");
string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var excluded = new HashSet<string> { "bin", "obj", "Tests", "Characterization", "RuntimeSmoke", "SmokeProject", "Assets", "Library", "Packages", ".git", "node_modules", ".venv", "venv", "tools" };
var owners = new Dictionary<string, List<(string Path, SyntaxNode Tree)>>();
foreach (string dir in Directory.GetDirectories(root))
{
    if (!Directory.EnumerateFiles(dir, "*.csproj").Any()) continue;
    string owner = Path.GetFileName(dir);
    var files = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
        .Where(p => !Path.GetRelativePath(dir, p).Split(Path.DirectorySeparatorChar).Any(excluded.Contains));
    owners[owner] = files.Select(p => (p, CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetRoot())).ToList();
}
var result = new List<object>();
var tabs = new List<object>();
var sources = new Dictionary<string, string>();
foreach (var (owner, files) in owners)
{
    // Include every source file of a registered tab, plus the local host/shared operation editor.
    bool registered = files.Any(f => f.Tree.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i =>
        i.Expression.ToString().EndsWith(".RegisterTab")) || f.Tree.ToFullString().Contains("_registerTab?.Invoke"));
    if (!registered && owner != "MainGameBlankMapAdd" && owner != "MainGameUiInputCapture") continue;
    foreach (var file in files)
        sources[Path.GetRelativePath(root, file.Path)] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file.Path)));
    var values = new Dictionary<string, ExpressionSyntax>();
    var wrappers = new Dictionary<string, int>();
    var helpParameters = new Dictionary<string, int>();
    var ownCalls = files.SelectMany(f => f.Tree.DescendantNodes().OfType<InvocationExpressionSyntax>())
        .Where(c => c.Expression is IdentifierNameSyntax).GroupBy(c => c.Expression.ToString())
        .ToDictionary(g => g.Key, g => g.ToList());
    foreach (var f in files)
    {
        foreach (var v in f.Tree.DescendantNodes().OfType<FieldDeclarationSyntax>().SelectMany(d => d.Declaration.Variables))
            if (v.Initializer != null && !values.ContainsKey(v.Identifier.Text)) values[v.Identifier.Text] = v.Initializer.Value;
        foreach (var m in f.Tree.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            int index = m.ParameterList.Parameters.ToList().FindIndex(p => p.Type?.ToString() == "string"
                && new[] { "label", "title", "caption" }.Contains(p.Identifier.Text));
            if (index >= 0 && m.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i => i.Expression.ToString().StartsWith("GUI") || i.Expression.ToString().StartsWith("Draw")))
            {
                wrappers[m.Identifier.Text] = index;
                int helpIndex = m.ParameterList.Parameters.ToList().FindIndex(p => new[] { "help", "tooltip" }.Contains(p.Identifier.Text));
                if (helpIndex >= 0) helpParameters[m.Identifier.Text] = helpIndex;
            }
        }
    }
    List<string> Labels(ExpressionSyntax expression, int depth = 0)
    {
        if (depth > 8 || expression == null) return new();
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) return new() { literal.Token.ValueText };
        if (expression is ParenthesizedExpressionSyntax paren) return Labels(paren.Expression, depth + 1);
        if (expression is IdentifierNameSyntax id)
        {
            var method = expression.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var local = method?.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .LastOrDefault(v => v.Identifier.Text == id.Identifier.Text && v.SpanStart < expression.SpanStart && v.Initializer != null);
            if (local != null) return Labels(local.Initializer.Value, depth + 1);
            if (method != null)
            {
                int parameter = method.ParameterList.Parameters.ToList().FindIndex(p => p.Identifier.Text == id.Identifier.Text);
                if (parameter >= 0 && ownCalls.TryGetValue(method.Identifier.Text, out var callers))
                    return callers.Where(c => c.ArgumentList.Arguments.Count > parameter)
                        .SelectMany(c => Labels(c.ArgumentList.Arguments[parameter].Expression, depth + 1)).Distinct().Take(256).ToList();
            }
            if (values.TryGetValue(id.Identifier.Text, out var value)) return Labels(value, depth + 1);
        }
        if (expression is ConditionalExpressionSyntax condition) return Labels(condition.WhenTrue, depth + 1).Concat(Labels(condition.WhenFalse, depth + 1)).Distinct().ToList();
        if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
        {
            var left = Labels(binary.Left, depth + 1); var right = Labels(binary.Right, depth + 1);
            if (left.Count > 0 && right.Count > 0) return left.SelectMany(l => right.Select(r => l + r)).Take(128).ToList();
            return new();
        }
        if (expression is ObjectCreationExpressionSyntax create && create.Type.ToString().EndsWith("GUIContent")) return Labels(create.ArgumentList?.Arguments.FirstOrDefault()?.Expression, depth + 1);
        if (expression is ArrayCreationExpressionSyntax array) return array.Initializer?.Expressions.SelectMany(e => Labels(e, depth + 1)).ToList() ?? new();
        if (expression is ImplicitArrayCreationExpressionSyntax implicitArray) return implicitArray.Initializer.Expressions.SelectMany(e => Labels(e, depth + 1)).ToList();
        if (expression is InitializerExpressionSyntax initializer) return initializer.Expressions.SelectMany(e => Labels(e, depth + 1)).ToList();
        if (expression is ElementAccessExpressionSyntax element) return Labels(element.Expression, depth + 1);
        return new();
    }
    foreach (var (path, tree) in files)
    foreach (var call in tree.DescendantNodes().OfType<InvocationExpressionSyntax>())
    {
        string expr = call.Expression.ToString();
        string name = call.Expression switch { MemberAccessExpressionSyntax member => member.Name.Identifier.Text, IdentifierNameSyntax id => id.Identifier.Text, _ => expr };
        var arguments = call.ArgumentList.Arguments;
        int index = -1;
        if (expr.EndsWith(".RegisterTab") && owner != "MainGameUnifiedUi" && arguments.Count >= 3)
            tabs.Add(new { owner, ids = Labels(arguments[0].Expression), labels = Labels(arguments[1].Expression), callback = arguments[2].Expression.ToString() });
        if (call.Ancestors().OfType<ConditionalAccessExpressionSyntax>().Any(c => c.Expression.ToString() == "_registerTab"))
            tabs.Add(new { owner, ids = new[] { "camera-look-control" }, labels = new[] { "首操作" }, callback = "DrawEmbedded" });
        if (expr.StartsWith("GUI.") || expr.StartsWith("GUILayout."))
        {
            int offset = expr.StartsWith("GUI.") ? 1 : 0;
            if (new[] { "Label", "Button", "RepeatButton", "Box" }.Contains(name)) index = offset;
            if (name == "Toggle") index = offset + 1;
            if (name == "Toolbar" || name == "SelectionGrid") index = offset + 1;
        }
        else if (wrappers.TryGetValue(name, out int labelIndex)) index = labelIndex;
        // Data-driven FloatSetting rows (label + help) are as important as literal GUI calls.
        if (name == "Add" && owner == "MainGameCameraLookControl" && arguments.Count == 8) index = 3;
        bool field = (expr.StartsWith("GUI.") || expr.StartsWith("GUILayout.")) && new[] { "HorizontalSlider", "VerticalSlider", "TextField", "TextArea", "PasswordField" }.Contains(name);
        if (!field && (index < 0 || arguments.Count <= index)) continue;
        ExpressionSyntax labelExpression = index >= 0 && arguments.Count > index ? arguments[index].Expression : null;
        var labels = Labels(labelExpression).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
        string help = null;
        if (labelExpression is ObjectCreationExpressionSyntax guiContent && guiContent.ArgumentList?.Arguments.Count > 1)
            help = Labels(guiContent.ArgumentList.Arguments[1].Expression).FirstOrDefault();
        if (helpParameters.TryGetValue(name, out int helpIndex) && arguments.Count > helpIndex)
            help = Labels(arguments[helpIndex].Expression).FirstOrDefault() ?? help;
        if (name == "Add" && owner == "MainGameCameraLookControl" && arguments.Count == 8)
            help = Labels(arguments[4].Expression).FirstOrDefault();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "";
        bool forwarded = labelExpression != null && call.Ancestors().Where(n => n is MethodDeclarationSyntax || n is LocalFunctionStatementSyntax)
            .Any(n => n.DescendantNodes().OfType<ParameterSyntax>().Any(p =>
                labelExpression.ToString() == p.Identifier.Text));
        bool hasTooltip = labelExpression is ObjectCreationExpressionSyntax contentCreate
            && contentCreate.Type.ToString().EndsWith("GUIContent") && contentCreate.ArgumentList?.Arguments.Count > 1;
        result.Add(new { owner, file = Path.GetRelativePath(root, path), line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            method, api = name, expression = labelExpression?.ToString(), labels, help, field, forwarded, hasTooltip });
    }
    var dataRows = files.SelectMany(f => f.Tree.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        .Select(c => new { Name = c.Identifier.Text, Parameters = c.ParameterList.Parameters.ToList(),
            Index = c.ParameterList.Parameters.ToList().FindIndex(p => p.Type?.ToString() == "string" && p.Identifier.Text == "label") })
        .Where(c => c.Index >= 0).GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.First());
    foreach (var (path, tree) in files)
    foreach (var create in tree.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
    {
        string typeName = create.Type.ToString().Split('.').Last();
        if (!dataRows.TryGetValue(typeName, out var row) || create.ArgumentList?.Arguments.Count <= row.Index) continue;
        var expression = create.ArgumentList.Arguments[row.Index].Expression;
        var labels = Labels(expression).Distinct().ToList();
        if (labels.Count == 0) continue;
        result.Add(new { owner, file = Path.GetRelativePath(root, path), line = create.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            method = typeName, api = "DataRow", expression = expression.ToString(), labels, help = (string)null, field = false, forwarded = false, hasTooltip = false });
    }
}
string target = Path.Combine(root, "MainGameUiHelp", "embedded_ui_audit.json");
string reportingSource = Path.Combine(root, "_shared", "UiHelpReporting.cs");
sources[Path.GetRelativePath(root, reportingSource)] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(reportingSource)));
File.WriteAllText(target, JsonSerializer.Serialize(new { tabs, sources, controls = result }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Embedded UI source audit: {tabs.Count} registrations, {result.Count} control sites; {target}");
