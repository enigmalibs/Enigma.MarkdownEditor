# Fenced code

A fence is coloured by the grammar its language names, in the same TextMate theme the editor pane
uses, and it follows the window from dark to light.

```csharp
public sealed class Note
{
    /* a comment
       that carries on over two lines */
    public string Title { get; init; } = string.Empty;
}
```

```json
{ "theme": "Dark", "vimEnabled": true }
```

```bash
rg --files | rg '\.md$' | wc -l
```

An info string can carry more than a language; only the first word is one.

```json title=settings.json
{ "showAllFiles": false }
```

A language nothing knows, and no language at all, both come out as plain monospace in the preview's
own body colour rather than as an error.

```klingon
nuqneH { }
```

```
no language at all
```

An indented block is code too:

    var indented = true;

A long line stays one line and goes behind the block's own scroll bar rather than wrapping:

```csharp
public static readonly string ALineLongEnoughToNeedTheHorizontalScrollBarInAPaneAboutAThirdOfTheWindowWide = "…";
```

Text after the fences.
