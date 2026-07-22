---
theme: default
title: How Exciting New C# Features Make Our Code Explicit
info: closed hierarchies and union types in .NET 11
highlighter: shiki
lineNumbers: false
transition: fade
colorSchema: light
drawings:
  enabled: false
addons:
  - "@slidev-polls/component"
# The slidev-polls backend host is injected from talk/.env.local (VITE_POLL_SERVER)
# via talk/setup/main.ts — it changes every talk, so it stays out of this file.
# See talk/README.md → Poll runbook.
---

# Find a weakness
```csharp 
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}

// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry",
    _ => $"✅ Source locked — advancing migration {migrationId} to Transform",
};
```

---
layout: center
---

# How Exciting New C# Features Make Our Code Explicit

## `closed` hierarchies & `union` types in .NET 11

<br>

*your name — your event, 2026*

<!--
[0:05] NOW introduce yourself — 30 seconds max, the audience already has the problem in their heads.

Frame the promise: "In the next 35 minutes: why that underscore is *required* today, what it costs us in reading and in evolution, and two brand-new C# constructs — closed hierarchies and union types — that each remove it. By the end you'll know exactly which one to reach for, because they are NOT the same feature."

Mention: everything shown today is in one repo — code, demos, these slides — link on the last slide. .NET 11 preview 6, so one disclaimer: syntax may still shift before November.
-->

--- 
---

# Migrate Distinct Comics heroes to Marble
- Marble has bought Distinct Comics 
- Our job is to migrate heroes from Distinct Comics to Marble

---

# Migration Tool Architecture

<<< ./snippets/architecture.mmd mermaid

---

# Find a weakness
```csharp {all|17}
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}

// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry",
    _ => $"✅ Source locked — advancing migration {migrationId} to Transform",
};
```

---

# So let's make it explicit

```csharp {17}
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}

// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry",
    _ => $"✅ Source locked — advancing migration {migrationId} to Transform",
};
```

---

# So let's make it explicit

```csharp {17|all}
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}

// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Locked => $"✅ Source locked — advancing migration {migrationId} to Transform",
    ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry",
};
```

---

# So let's make it explicit
```csharp
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}

// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
//                                          ~~~~~~
// error CS8509: The switch expression does not handle all possible values
// of its input type (it is not exhaustive).
{
    ILockSource.Response.Locked => $"✅ Source locked — advancing migration {migrationId} to Transform",
    ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry",
};
```

---

# So let's make it explicit
```csharp {18}
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}

// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Locked => $"✅ Source locked — advancing migration {migrationId} to Transform",
    ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry",
    _ => throw new ArgumentOutOfRangeException(nameof(migrationId), migrationId, null),
};
```

---

# Why is the `_` even there?

```csharp
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}

// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Locked => $"✅ Source locked — advancing migration {migrationId} to Transform",
    ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry",
    _ => throw new ArgumentOutOfRangeException(nameof(migrationId), migrationId, null),
};
```

<!--
[0:06] Key teaching moment — the underscore is not laziness, it is REQUIRED.

"Response is a public abstract record. Any assembly — another project in this solution, a NuGet package — can derive from it. So when I switch over it, the compiler is RIGHT: my two named cases don't cover everything, because the set of cases is open. The underscore isn't sloppiness. It's the only honest answer to a type that can't state its own boundaries."

"The switch was never the problem. The TYPE is the problem — it's open, and my domain is closed. The language just had no way to say so. Until now."

Last click — the foreshadow: "And before you file this under 'adapter problem': the Result type — completed-or-failed, the one every handler here returns — has the exact same shape, and its combinators carry the exact same underscore. We'll come back to it; it gets its own feature."
-->

---

# Migration Tool Architecture

<v-switch>
<template #0>

<<< ./snippets/architecture.mmd mermaid

</template>
<template #1>

<<< ./snippets/architecture-adapters-highlighted.mmd mermaid

### Can inherit from `public` classes in **Mt.Domain**

</template>
</v-switch>


<!--
[0:10] Keep this to TWO minutes — it's context, not a thesis. Resist the architecture tangent.

Say: "Hero migration pipeline: lock the hero in Distinct Comics and in Marble, transform, unlock, tell the user. Each step is a handler. Handlers talk to the outside world through ports — interfaces the domain owns. Adapters implement them: Postgres, Distinct Comics, Marble."

The one sentence that matters for the rest of the talk: "Every port answers with a Response record — one subtype per business outcome. Proceed or DoNotProceed. Locked or Faulted. Scheduled or Exhausted. Which means every handler is full of switches over open hierarchies — every one of them wearing an underscore."

Click — Mt.DistinctComics and Mt.Marble light up: "And note these two projects. They reference Mt.Domain, so they can inherit from any public class in it — including every Response type. Remember that; it's why the compiler was right to complain."

"So every decision in this codebase carries the reading tax from the opening. Let's remove it."
-->

---

### Mt.DistinctComics inherits from `ILockSource.Response`

```csharp{all|4,14}
// Mt.Domain
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}

// Mt.DistinctComics
public sealed record Throttled(TimeSpan RetryAfter) : ILockSource.Response;

public sealed class DistinctComicsLockSource : ILockSource
{
    public ILockSource.Response Handle(long migrationId) => new Throttled(TimeSpan.FromMinutes(5));
}
```

---

# The `closed` newcomer
<v-click>

- Only types in the same assembly can inherit

</v-click>

<v-click>

- Assumes `abstract`

</v-click>


---

# The `closed` newcomer
````md magic-move
```csharp
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}
```
```csharp
// Mt.Domain.ILockSource
public interface ILockSource
{
    Response Handle(long migrationId);

    public closed record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}
```
````

---

# The `switch` loses the discard
````md magic-move
```csharp
// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Locked
        => $"✅ Source locked — advancing migration {migrationId} to Transform",

    ILockSource.Response.Faulted(var reason)
        => $"⏰ Lock faulted ({reason}) — scheduling retry",

    _ => throw new ArgumentOutOfRangeException(nameof(migrationId), …)
};
```
```csharp
// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Locked
        => $"✅ Source locked — advancing migration {migrationId} to Transform",

    ILockSource.Response.Faulted(var reason)
        => $"⏰ Lock faulted ({reason}) — scheduling retry",
};
```
````

---

# Assemblies that inherit no longer compile

```csharp
// Mt.DistinctComics — a different assembly
public sealed record Throttled(TimeSpan RetryAfter) : ILockSource.Response;
//                   ~~~~~~~~~
// error CS9382: 'Throttled': cannot use a closed type 'ILockSource.Response'
// from another assembly as a base type.
```

<div v-click class="absolute bottom-10 right-10 flex items-start gap-3">
  <div class="bg-white border-2 border-black rounded-2xl px-4 py-2 text-2xl font-bold shadow-lg self-start -rotate-3">
    Great success!
  </div>
  <img src="/borat.jpg" class="h-52 rounded-lg shadow-xl rotate-2" alt="Borat" />
</div>


---
layout: center
---

<div class="flex items-center justify-center gap-8">
  <img src="/billymays.jpg" class="h-90 rounded-lg shadow-xl -rotate-2" alt="Billy Mays" />
  <div class="bg-white border-2 border-black rounded-2xl px-6 py-4 text-4xl font-bold shadow-lg rotate-2">
    But wait — there's more!
  </div>
</div>

---

# The union type
- Alternative way to exhaust the switch
- Works on types that have nothing to do with each other

```csharp
public interface ILockSource
{
    Response Handle(long migrationId);

    public abstract record Response
    {
        /// <summary>Source locked the hero.</summary>
        public sealed record Locked : Response;

        /// <summary>Source did not lock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
```

---

# The union type
- Alternative way to exhaust the switch
- Works on types that have nothing to do with each other

```csharp
public interface ILockSource
{
    Response Handle(long migrationId);

    public union Response(Response.Locked, Response.Faulted)
    {
        /// <summary>Source locked the hero.</summary>
        public sealed record Locked;

        /// <summary>Source did not lock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason);
    }
}
```
<v-click>

 - Works exactly as the version with `closed`

</v-click>

<div v-click class="absolute bottom-10 right-10 flex items-start gap-3">
  <div class="bg-white border-2 border-black rounded-2xl px-4 py-2 text-2xl font-bold shadow-lg rotate-2 self-start">
    What the hell?
  </div>
  <img src="/jackiechan.jpg" class="h-44 rounded-lg shadow-xl -rotate-2" alt="Jackie Chan, arms spread in confusion" />
</div>

<!--
Click 1 — the bullet: "And it works exactly like the closed version. Same exhaustiveness, same CS8509, same everything."

Click 2 — Jackie appears: "Which means C# now ships TWO ways of doing the same thing. I don't like that. I shouldn't have to choose between two constructs for one job. What the hell?"

Let the laugh land, then the bridge: "It turns out they are NOT the same thing — they differ on exactly one axis. Next slide."
-->

---

# When to use `union` and when to use `closed`
- If the types represent different versions of the same thing? => `closed`
- If the types represent completely different things => `union`

<div v-click class="absolute bottom-10 right-10 flex items-start gap-3">
  <div class="bg-white border-2 border-black rounded-2xl px-4 py-2 text-2xl font-bold shadow-lg -rotate-2 self-start">
    That's way too vague!
  </div>
  <img src="/facepalm.jpg" class="h-48 rounded-lg shadow-xl rotate-1" alt="Facepalm statue" />
</div>

<!--
Read the two bullets out loud, slowly — let the room FEEL how mushy they are. "Versions of the same thing"… "completely different things"… according to whom?

Click — the facepalm: "I know. That's way too vague. You can argue any type into either bucket."

The bridge: "So let me give you a test that isn't vague. It's about DATA. If the cases share data, only one of these constructs can even express that. Let me show you." → next slide, the INotifyCompletion Request example.
-->

---

# Which one is correct?

<div class="grid grid-cols-2 gap-4 text-sm">

```csharp
// closed
public interface ILockSource
{
    Response Handle(long migrationId);

    public closed record Response
    {
        public sealed record Locked : Response;
        public sealed record Faulted(string Reason) : Response;
    }
}
```

```csharp
// union
public interface ILockSource
{
    Response Handle(long migrationId);

    public union Response(Response.Locked, Response.Faulted)
    {
        public sealed record Locked;
        public sealed record Faulted(string Reason);
    }
}
```

</div>

<div class="flex justify-center mt-2">
  <div class="w-44">
    <PollQr slug="csharp15" />
  </div>
</div>

<!-- PollQr is display-only; this hidden panel is what auto-opens the question
     when the slide shows (needs the toolbar sign-in). The poll identity lives in
     talk/.env.local via components/Poll.vue — see README → Poll runbook. -->
<div style="display: none" aria-hidden="true">
  <Poll />
</div>

<!--
LIVE POLL — the audience scans the QR and votes on their phones.

The question opens AUTOMATICALLY when this slide shows (hidden PollResults panel above — requires the toolbar sign-in). Fallback: open the admin UI on your phone and flip the question to ACTIVE. Either way the admin UI doubles as YOUR live tally view; the room sees no numbers yet.

Narrate while they vote: "Both compile. Both are exhaustive. Both delete the discard. So which one is the right way to write ILockSource? You have thirty seconds."

When the flow of votes dries up in the admin view → advance. The next slide reveals the tally and closes the question.

FALLBACK if the network is down (phones show spinners): "The network has voted 'abstain'. Old school then — hands up for closed… hands up for union… hands up for 'how should I know?'" (Thank the third group — they're the honest ones.)
-->

---

# The verdict

<div class="flex justify-center">
  <Poll />
</div>

<!--
The reveal — advancing to this slide closes the question and the tally animates in on the big screen.

React to whatever the room actually said. Whichever way it leans: "Here's the thing — there was no right answer on that slide. Both are correct. And that's exactly the problem." → next slide, the when-to-use bullets (and the facepalm).

If the poll was skipped (network fallback), skip this slide too — you already have the hands result.
-->

---

# `closed` support shared data

```csharp
namespace Mt.Domain;

public interface INotifyCompletion
{
    void Handle(Request request);

    public closed record Request(Id MigrationId)
    {
        public sealed record Migrated(Id MigrationId) : Request(MigrationId);
        public sealed record Cancelled(Id MigrationId) : Request(MigrationId);
    }
}
```

<!--
"This is the completion notification. Both outcomes — migrated, cancelled — carry the migration id, hoisted into the BASE record's primary constructor. A union has no base to hoist anything into — this type is only expressible as a hierarchy."

Point at the bottom: "And the consumer reads request.MigrationId before any switch — it doesn't care which case it holds. Shared data, used polymorphically. That's why Response types get closed, not union."
-->

---

# `union` does not support shared data

```csharp
namespace Mt.Domain;

public interface INotifyCompletion
{
    void Handle(Request request);

    public closed record Request(Id MigrationId)
    {
        public sealed record Migrated(Id MigrationId) : Request(MigrationId);
//                                                  ~~~~~~~~~~~~~~~~~~~~
// error CS0509: 'Request.Migrated': cannot derive from sealed type 'Request'
        public sealed record Cancelled(Id MigrationId) : Request(MigrationId);
//                                                  ~~~~~~~~~~~~~~~~~~~~
// error CS0509: 'Request.Migrated': cannot derive from sealed type 'Request'
    }
}
```

---

# When to use `union` and when to use `closed`
<v-click>

- If the types share data? => `closed`
</v-click>

<v-click>

- If the types does not share data? => `union`

</v-click>

<v-click>

- House style matters

</v-click>

<div v-click class="absolute bottom-10 right-10 flex items-start gap-3">
  <div class="bg-white border-2 border-black rounded-2xl px-4 py-2 text-2xl font-bold shadow-lg -rotate-2 self-start">
    Show them an example with <code>union</code>!
  </div>
  <img src="/madstorgersen.jpg" class="h-48 rounded-lg shadow-xl rotate-2" alt="Mads Torgersen mid-proclamation on stage" />
</div>

---

# The `Result` pattern

```csharp{all|3}
public interface ILockSource
{
    Result<Response> Handle(Id migrationId);

    public closed record Response
    {
        /// <summary>Source locked the hero.</summary>
        public sealed record Locked : Response;

        /// <summary>Source did not lock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
```

---

# The `Result` pattern

```csharp
public sealed class Handler(
    ILockSource lockSource,
    ILockTarget lockTarget,
    INotifyCompletion notifyCompletion)
{
    public void Handle(Id migrationId)
    {
        var result = lockSource.Handle(migrationId)
            .Then(_ => lockTarget.Handle(migrationId))
            .Then(_ => notifyCompletion.Handle(new INotifyCompletion.Request.Migrated(migrationId)));

        if (result is Failed failed)
        {
            Console.WriteLine($"Failed {failed}");
        } else
        {
            Console.WriteLine($"Successfully locked source and target");
        }
    }
}
```

<v-click>

- `Then` clause only executes when `Completed<T>` is returned

</v-click>

<v-click>

- `Failed` propagates

</v-click>

---

# The `Result` pattern
```csharp
public abstract record Result<T>;
public sealed record Completed<T>(T Value) : Result<T>;
public sealed record Failed<T>(string Reason) : Result<T>;

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed<T>(var reason) => new Failed<TRes>(reason),
                _ => throw new Exception()
            };
    }
}
```

---

# The `Result` pattern
```csharp
public abstract record Result<T>;
public sealed record Completed<T>(T Value) : Result<T>;
public sealed record Failed<T>(string Reason) : Result<T>;

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed<T>(var reason) => new Failed<TRes>(reason),
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```
---

# The `Result` pattern
```csharp
public abstract record Result<T>;
public sealed record Completed<T>(T Value) : Result<T>;
public sealed record Failed<T>(string Reason) : Result<T>;

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

---

# The `Result` pattern
```csharp
public abstract record Result<T>;
public sealed record Completed<T>(T Value) : Result<T>;
public sealed record Failed<T>(string Reason) : Result<T>; // pointless T

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

---

# The `Result` pattern - with `union`
```csharp {all|1}
public abstract record Result<T>;
public sealed record Completed<T>(T Value) : Result<T>;
public sealed record Failed<T>(string Reason) : Result<T>; // pointless T

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

---

# The `Result` pattern - with `union`
```csharp {1|2,3}
public union Result<T>(Completed<T>, Failed);
public sealed record Completed<T>(T Value) : Result<T>;
public sealed record Failed<T>(string Reason) : Result<T>; // pointless T

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```
---

# The `Result` pattern - with `union`
```csharp {2,3|3}
public union Result<T>(Completed<T>, Failed);
public sealed record Completed<T>(T Value);
public sealed record Failed<T>(string Reason); // pointless T

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

---

# The `Result` pattern - with `union`
```csharp {3|13}
public union Result<T>(Completed<T>, Failed);
public sealed record Completed<T>(T Value);
public sealed record Failed(string Reason);

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

---

# The `Result` pattern - with `union`
```csharp {13|14}
public union Result<T>(Completed<T>, Failed);
public sealed record Completed<T>(T Value);
public sealed record Failed(string Reason);

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed f => f,
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```
---

# The `Result` pattern - with `union`
```csharp {none|all}
public union Result<T>(Completed<T>, Failed);
public sealed record Completed<T>(T Value);
public sealed record Failed(string Reason);

public static class Extensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next)
            => result switch
            {
                Completed<T>(var value) => next(value),
                Failed f => f
            };
    }
}
```

<div v-click class="absolute bottom-10 right-10 flex items-end gap-3">
  <img src="/chefskiss.jpg" class="h-56 rounded-lg shadow-xl -rotate-2" alt="Cartoon chef making the chef's-kiss gesture" />
</div>

<!--
The code fully reveals — the whole `Then` combinator, no pointless default case, exhaustive on the two union members.

Click — the chef appears: "And THAT is the whole pattern. Two cases, exhaustively matched, no `_ => throw` to apologise for. Chef's kiss."
-->

---

# Why is this important?
<v-click>

 - Less verbose code
</v-click>
<v-click>

 - Signals intent
</v-click>
<v-click>

 - Make it harder for developers to missuse code
</v-click>

---
layout: center
---

# Thank you

<div class="flex items-center justify-center gap-12">

<div>

**Everything from today — code, demos, this deck:**
`github.com/kimrs/fagkveld-knirkefritt

- .NET **11 preview 6** · `<LangVersion>preview</LangVersion>` · syntax may shift before GA
- The union & closed-hierarchy design: `github.com/dotnet/csharplang` — *Type Unions* proposal
- Works because of: `TreatWarningsAsErrors` (or at least promote **CS8509**)

</div>

<FeedbackQr url="https://forms.gle/REPLACE_WITH_REAL_FORM_ID" caption="Scan to leave feedback" />

</div>

<!--
Q&A prep — likely questions and your answers:

1. "Why not the OneOf library?" — OneOf gives you a container TODAY and it's good. The language feature adds: exhaustiveness enforced at every switch site by name (OneOf's .Match forces arity, but switch/patterns integration is native), real pattern matching, no T0/T1 noise, and no dependency. If you're on .NET 8/10 today: OneOf is the bridge, this is the destination.

2. "When does this ship?" — .NET 11 GA is November 2026; these are preview-6 features behind LangVersion preview. Syntax risk is real but shrinking; semantics (exhaustiveness, closed = same-assembly) have been stable across previews.

3. "Isn't closed just Kotlin's sealed class?" — Yes, deliberately (and union is Rust's enum / F#'s DU). C# is unusual in shipping BOTH, because it has 20 years of hierarchies to retrofit AND wants the honest choice type for new code.

4. "Performance of unions?" — the wrapper is a struct holding an object reference; class cases allocate as before, the wrapper itself doesn't. Measure before worrying.

5. "What about closed enums / closed interfaces?" — in the proposal family, not in preview 6; don't promise.

6. "Serialization?" — untested territory, expect the wrapper to leak exactly like Assert.IsType did. Test before shipping unions across a wire.
-->
