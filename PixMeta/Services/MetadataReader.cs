using ImageMagick;
using PixMeta.Models;
using System.Text.Json;

namespace PixMeta.Services;

public static class MetadataReader
{
    public static ImageMetadata Read(string path)
    {
        var r = new ImageMetadata();
        using var image = new MagickImage(path);
        r.ActualFormat = image.Format.ToString();
        var ext = Path.GetExtension(path).ToLowerInvariant();
        r.ExtensionMismatch = (ext == ".png" && image.Format != MagickFormat.Png)
            || (ext is ".jpg" or ".jpeg" && image.Format != MagickFormat.Jpeg)
            || (ext == ".webp" && image.Format != MagickFormat.WebP);

        var attrs = image.AttributeNames.ToDictionary(x => x, x => image.GetAttribute(x) ?? "",
            StringComparer.OrdinalIgnoreCase);
        var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (image.Format is MagickFormat.Jpeg or MagickFormat.WebP)
            foreach (var item in JpegExifReader.Read(path)) extra[item.Key] = item.Value;

        r.Raw = string.Join("\n", attrs.Select(x => $"{x.Key}: {x.Value}")
            .Concat(extra.Select(x => $"{x.Key}: {x.Value}")));

        // Preview Plus writes PNG text chunks, or a JSON envelope in JPG/WEBP EXIF.
        bool hasParameters = attrs.TryGetValue("parameters", out var parameters)
            || attrs.TryGetValue("png:parameters", out parameters);
        bool hasComfyPrompt = attrs.TryGetValue("prompt", out var comfyPrompt)
            || attrs.TryGetValue("png:prompt", out comfyPrompt);
        bool hasWorkflow = attrs.TryGetValue("workflow", out var workflow)
            || attrs.TryGetValue("png:workflow", out workflow);
        // Preview Plus PNG / JSON OFF uses custom metadata rather than the
        // A1111 `parameters` chunk, to prevent automatic synthetic graph import.
        bool hasPreviewPlusValues = attrs.TryGetValue("preview_plus_parameters", out var ppOnly)
            || attrs.TryGetValue("png:preview_plus_parameters", out ppOnly);

        if (hasParameters)
        {
            r.Source = attrs.ContainsKey("preview_plus_parameters") || attrs.ContainsKey("png:preview_plus_parameters")
                ? "ComfyUI / Preview Plus" : "A1111 / Forge";
            ParseA1111(parameters!, r);
            if (attrs.TryGetValue("preview_plus_parameters", out var pp) ||
                attrs.TryGetValue("png:preview_plus_parameters", out pp))
                ParsePreviewPlusParameters(pp, r);
            if (hasComfyPrompt) ParseComfy(comfyPrompt!, r, (int)image.Width, (int)image.Height);
        }
        else if (hasPreviewPlusValues)
        {
            r.Source = "ComfyUI / Preview Plus";
            ParsePreviewPlusParameters(ppOnly!, r);
        }
        else if (hasComfyPrompt)
        {
            r.Source = "ComfyUI";
            ParseComfy(comfyPrompt!, r, (int)image.Width, (int)image.Height);
        }
        else
        {
            string comment = extra.TryGetValue("EXIF UserComment", out var value)
                ? value : attrs.GetValueOrDefault("exif:UserComment", "");
            if (ParsePreviewPlusEnvelope(comment, r, (int)image.Width, (int)image.Height,
                out var envelopeWorkflow))
                hasWorkflow = envelopeWorkflow;
            else if (ParseComfyJpgMetadata(comment, r, (int)image.Width, (int)image.Height,
                out var jpgWorkflow))
                hasWorkflow = jpgWorkflow;
            else if (LooksLikeGenerationComment(comment))
            {
                r.Source = LooksLikeA1111(comment) ? "A1111 / Forge" : "A1111-compatible / post-processed";
                ParseA1111(comment, r);
            }
        }

        // Standard EXIF Software tag identifies Preview Plus even when its
        // UserComment is plain A1111-compatible text (JPG / JSON OFF).
        if (extra.TryGetValue("EXIF Software", out var generator) &&
            generator.Contains("Preview Plus", StringComparison.OrdinalIgnoreCase))
            r.Source = "ComfyUI / Preview Plus";

        // From v0.6 JPG / JSON ON, the visual graph is in EXIF
        // ImageDescription; UserComment remains Forge-compatible plain text.
        if (extra.TryGetValue("EXIF ImageDescription", out var graphDescription)
            && graphDescription.TrimStart().StartsWith('{'))
        {
            try
            {
                using var graphDoc = JsonDocument.Parse(graphDescription);
                if (graphDoc.RootElement.ValueKind == JsonValueKind.Object
                    && graphDoc.RootElement.TryGetProperty("workflow", out var graphNode)
                    && graphNode.ValueKind == JsonValueKind.Object)
                    hasWorkflow = true;
            }
            catch (JsonException) { /* Non-JSON EXIF description. */ }
        }
        // Native ComfyUI WebP workflow is stored as an EXIF ASCII Make tag:
        // "workflow:{JSON}". Preview Plus uses this when JSON ON; OFF has no tag.
        // The plain UserComment remains readable in Forge in both modes.
        if (image.Format == MagickFormat.WebP &&
            extra.TryGetValue("EXIF Make", out var webpWorkflow) &&
            webpWorkflow.StartsWith("workflow:", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(webpWorkflow["workflow:".Length..]);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    hasWorkflow = true;
            }
            catch (JsonException) { /* Invalid or unrelated EXIF data. */ }
        }
        if (hasWorkflow) r.Fields["Workflow"] = "Embedded";
        if (IsGrok(extra))
        {
            r.Source = "Grok / xAI";
            r.Fields["AI signature"] = "Present";
            if (extra.TryGetValue("EXIF Artist", out var artist) && artist.Length > 0)
                r.Fields["Asset ID"] = artist;
        }

        foreach (var pair in attrs)
        {
            var key = pair.Key.ToLowerInvariant();
            bool knownMetadata = key is "prompt" or "png:prompt" or "workflow" or "png:workflow"
                or "preview_plus_parameters" or "png:preview_plus_parameters" or "parameters" or "png:parameters"
                or "preview_plus_infotext" or "png:preview_plus_infotext";
            if (knownMetadata) continue;
            if (r.Prompt.Length == 0 && (key.Contains("prompt") || key.Contains("description"))
                && !key.Contains("negative") && pair.Value.Length < 100000 && !LooksLikeJson(pair.Value))
                r.Prompt = pair.Value;
            if (r.NegativePrompt.Length == 0 && key.Contains("negative") && pair.Value.Length < 100000
                && !LooksLikeJson(pair.Value))
                r.NegativePrompt = pair.Value;
        }

        if (r.Source == "Unknown" && r.HasGenerationData) r.Source = "Generic / unknown AI metadata";
        if (r.Source is "A1111 / Forge" or "A1111-compatible / post-processed" or "ComfyUI / Preview Plus")
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(r.Prompt)) lines.Add(r.Prompt.Trim());
            if (!string.IsNullOrWhiteSpace(r.NegativePrompt)) lines.Add("Negative prompt: " + r.NegativePrompt.Trim());
            var fields = r.Fields.Where(x => x.Key != "Workflow").ToArray();
            if (fields.Length > 0) lines.Add(string.Join(", ", fields.Select(x => $"{x.Key}: {x.Value}")));
            r.ExportData = string.Join("\n", lines);
            r.ExportKind = r.Source == "ComfyUI / Preview Plus"
                ? "ComfyUI generation data" : "Forge generation data";
        }
        else if (r.Source == "ComfyUI" && hasWorkflow)
        {
            r.ExportData = workflow ?? "";
            r.ExportKind = "ComfyUI workflow";
        }
        return r;
    }

    static bool LooksLikeJson(string text) => text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('[');
    static bool IsGrok(Dictionary<string, string> extra) => extra.Values.Any(v =>
        v.Contains("Signature:", StringComparison.OrdinalIgnoreCase))
        && extra.TryGetValue("EXIF Artist", out var artist) && Guid.TryParse(artist, out _);
    static bool LooksLikeA1111(string text) => text.Contains("Steps:", StringComparison.OrdinalIgnoreCase)
        && (text.Contains("Seed:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Sampler:", StringComparison.OrdinalIgnoreCase));
    static bool LooksLikeGenerationComment(string text) => LooksLikeA1111(text)
        || text.Contains("Negative prompt:", StringComparison.OrdinalIgnoreCase);

    static bool ParsePreviewPlusEnvelope(string comment, ImageMetadata r, int width, int height,
        out bool hasWorkflow)
    {
        hasWorkflow = false;
        if (!LooksLikeJson(comment)) return false;
        try
        {
            using var doc = JsonDocument.Parse(comment);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                (!root.TryGetProperty("parameters", out _) && !root.TryGetProperty("preview_plus_parameters", out _)))
                return false;

            r.Source = "ComfyUI / Preview Plus";
            if (root.TryGetProperty("parameters", out var paramText) && paramText.ValueKind == JsonValueKind.String)
                ParseA1111(paramText.GetString() ?? "", r);
            if (root.TryGetProperty("preview_plus_parameters", out var jsonParams))
                ParsePreviewPlusParameters(jsonParams.ValueKind == JsonValueKind.String
                    ? jsonParams.GetString() ?? "" : jsonParams.GetRawText(), r);
            if (root.TryGetProperty("prompt", out var graph))
                ParseComfy(graph.ValueKind == JsonValueKind.String ? graph.GetString() ?? "" : graph.GetRawText(),
                    r, width, height);
            hasWorkflow = root.TryGetProperty("workflow", out var wf) && wf.ValueKind != JsonValueKind.Null;
            return true;
        }
        catch (JsonException) { return false; }
    }

    // ComfyUI-JPG-Metadata embeds {"prompt": {...}, "workflow": {...}} in EXIF UserComment.
    // The prompt object is the executable graph; the workflow is the UI graph.
    static bool ParseComfyJpgMetadata(string comment, ImageMetadata r, int width, int height,
        out bool hasWorkflow)
    {
        hasWorkflow = false;
        if (!LooksLikeJson(comment)) return false;
        try
        {
            using var doc = JsonDocument.Parse(comment);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("prompt", out var graph) || graph.ValueKind != JsonValueKind.Object)
                return false;
            // Distinguish a Comfy execution graph from an unrelated JSON comment.
            bool isComfy = graph.EnumerateObject().Any(node =>
                node.Value.ValueKind == JsonValueKind.Object &&
                node.Value.TryGetProperty("class_type", out _));
            if (!isComfy) return false;
            r.Source = "ComfyUI / JPG Metadata";
            ParseComfy(graph.GetRawText(), r, width, height);
            hasWorkflow = root.TryGetProperty("workflow", out var wf) &&
                wf.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
            return true;
        }
        catch (JsonException) { return false; }
    }

    static void ParsePreviewPlusParameters(string json, ImageMetadata r)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            foreach (var field in doc.RootElement.EnumerateObject())
            {
                var value = Scalar(field.Value);
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (field.Name == "Prompt") r.Prompt = value;
                else if (field.Name == "Negative prompt") r.NegativePrompt = value;
                else r.Fields[field.Name] = value;
            }
        }
        catch (JsonException) { /* Ignore malformed optional metadata. */ }
    }

    // A1111 metadata may start with a blank positive prompt, followed by a negative
    // prompt and a final line beginning with Size instead of Steps.
    static void ParseA1111(string text, ImageMetadata r)
    {
        text = text.Trim('\0', '\r', '\n', ' ');
        var lines = text.Replace("\r\n", "\n").Split('\n');
        int paramsIndex = -1;
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i].TrimStart();
            bool beginsWithParameter = line.StartsWith("Steps:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Size:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Sampler:", StringComparison.OrdinalIgnoreCase);
            if (beginsWithParameter && LooksLikeA1111(line)) { paramsIndex = i; break; }
        }
        string main = paramsIndex >= 0 ? string.Join("\n", lines.Take(paramsIndex)) : text;
        int neg = main.IndexOf("Negative prompt:", StringComparison.OrdinalIgnoreCase);
        r.Prompt = (neg < 0 ? main : main[..neg]).Trim();
        if (neg >= 0) r.NegativePrompt = main[(neg + "Negative prompt:".Length)..].Trim();
        if (paramsIndex >= 0)
        {
            foreach (var item in lines[paramsIndex].Split(','))
            {
                var pair = item.Split(':', 2);
                if (pair.Length == 2 && !string.IsNullOrWhiteSpace(pair[0]))
                    r.Fields[pair[0].Trim()] = pair[1].Trim();
            }
        }
    }

    static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value)) return true;
        value = default;
        return false;
    }

    static string? Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
        _ => null
    };

    static JsonElement GetLinkedNode(JsonElement graph, JsonElement link, out string id)
    {
        id = "";
        if (link.ValueKind != JsonValueKind.Array || link.GetArrayLength() != 2) return default;
        id = Scalar(link[0]) ?? "";
        return TryProp(graph, id, out var node) ? node : default;
    }

    static string FindText(JsonElement graph, JsonElement reference, int depth, HashSet<string> seen)
    {
        if (depth > 20) return "";
        var node = GetLinkedNode(graph, reference, out var id);
        if (node.ValueKind != JsonValueKind.Object || !seen.Add(id) || !TryProp(node, "inputs", out var inputs))
            return "";

        // Text must come from the conditioning branch connected to KSampler.
        // PromptLora stores its positive text under "prompt", CLIPTextEncode under "text".
        var texts = new List<string>();
        foreach (string key in new[] { "prompt", "text", "text_g", "text_l", "positive_prompt", "negative_prompt" })
        {
            if (TryProp(inputs, key, out var value))
            {
                var literal = Scalar(value);
                if (!string.IsNullOrWhiteSpace(literal) && !texts.Contains(literal)) texts.Add(literal);
            }
        }
        if (texts.Count > 0) return string.Join("\n", texts);

        foreach (var input in inputs.EnumerateObject())
        {
            var name = input.Name.ToLowerInvariant();
            if (!(name.Contains("conditioning") || name is "positive" or "negative" or "text" or "prompt"))
                continue;
            var found = FindText(graph, input.Value, depth + 1, seen);
            if (!string.IsNullOrWhiteSpace(found)) return found;
        }
        return "";
    }

    static void ParseComfy(string graphText, ImageMetadata r, int width, int height)
    {
        try
        {
            using var document = JsonDocument.Parse(graphText);
            var graph = document.RootElement;
            if (graph.ValueKind != JsonValueKind.Object) return;
            JsonElement sampler = default;
            foreach (var item in graph.EnumerateObject())
            {
                if (!TryProp(item.Value, "class_type", out var cls)) continue;
                var kind = Scalar(cls) ?? "";
                if (kind.Contains("KSampler", StringComparison.OrdinalIgnoreCase))
                {
                    sampler = item.Value;
                    break;
                }
            }
            if (sampler.ValueKind == JsonValueKind.Object && TryProp(sampler, "inputs", out var inputs))
            {
                foreach (var (name, key) in new[] {
                    ("Steps", "steps"), ("Sampler", "sampler_name"), ("Scheduler", "scheduler"),
                    ("CFG", "cfg"), ("Seed", "seed"), ("Denoise", "denoise") })
                    if (TryProp(inputs, key, out var value) && Scalar(value) is string literal)
                        r.Fields[name] = literal;

                if (TryProp(inputs, "positive", out var positive))
                {
                    var text = FindText(graph, positive, 0, new HashSet<string>());
                    if (text.Length > 0) r.Prompt = text;
                }
                if (TryProp(inputs, "negative", out var negative))
                {
                    var text = FindText(graph, negative, 0, new HashSet<string>());
                    if (text.Length > 0) r.NegativePrompt = text;
                }
            }
            r.Fields["Size"] = $"{width}x{height}";
            foreach (var item in graph.EnumerateObject())
            {
                if (!TryProp(item.Value, "class_type", out var cls) || !TryProp(item.Value, "inputs", out var modelInputs))
                    continue;
                string kind = Scalar(cls) ?? "";
                string key = kind.Contains("CheckpointLoader", StringComparison.OrdinalIgnoreCase)
                    ? "ckpt_name" : kind.Contains("UNETLoader", StringComparison.OrdinalIgnoreCase)
                        ? "unet_name" : "";
                if (key.Length > 0 && TryProp(modelInputs, key, out var model) && Scalar(model) is string modelName)
                {
                    r.Fields["Model"] = modelName;
                    break;
                }
            }
        }
        catch (JsonException) { /* Unknown node syntax; retain other metadata. */ }
    }
}
