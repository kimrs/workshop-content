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
<!--
Er det noen av dere som ser en svakhet med denne kodesnutten?
--->

---
layout: center
---

# How Exciting New C# Features Make Our Code Explicit

## `closed` hierarchies & `union` types in C# 15

<br>

*your name — your event, 2026*

<!--
Jeg har laget en presentasjon for dere.
Vi skal ta en titt på to nye features i C# 15
og utforske hvordan disse kan forbedre koden vår.
- Den første vi skal se på er closed
- Den andre er union types
Men først skal jeg introdusere dere for domenet
-->

--- 
---

# Migrate Distinct Comics heroes to Marble
- Marble has bought Distinct Comics 
- Our job is to migrate heroes from Distinct Comics to Marble

<!--
Caset vi skal jobbe med er at Marvel har kjøpt DC Comics
- De skal nå migrere superhelter fra DC universet til Marvel universet
- For å få til dette har de hyret oss inn til å lage verktøyet for å migrere
-->
---

# Migration Tool Architecture

<<< ./snippets/architecture.mmd mermaid

<!--
Her er en oversikt over hvordan appen vår ser ut i dag
- Vi strukturerer koden vår etter Ports & Adapters arkitektur
- Vi har tre porter
    - ILockSource låser DC Comics systemet
    - ILockTarget låser Marvel systemet
    - INotifyCompletion varsler når superheltene er migrert
- Koden består av tre dll-er.
    - Mt.Domain som inneholder porter og foretningslogikk
    - Mt.DistinctComics som kommuniserer med det gamle systemet
    - Mt.Marble som kommuniserer med det nye systemet
-->
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

<!--
Her er en oversikt over hvordan appen vår ser ut i dag
- Vi strukturerer koden vår etter Ports & Adapters arkitektur
- Vi har tre porter
    - ILockSource låser DC Comics systemet
    - ILockTarget låser Marvel systemet
    - INotifyCompletion varsler når superheltene er migrert
- Koden består av tre dll-er.
    - Mt.Domain som inneholder porter og foretningslogikk
    - Mt.DistinctComics som kommuniserer med det gamle systemet
    - Mt.Marble som kommuniserer med det nye systemet
-->
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

<!--
Her er kodesnutten jeg viste dere tidligere. Den viser ILockSource porten og et utdrag der den blir brukt.
ILockSource kan svare med to forskjellige responser. Locked eller Faulted.
- Locked vil si at det gamle systemet ble låst
- Faulted vil si at det gamle systemet ikke klarte å låse seg. I dette tilfellet ønsker vi å vente litt før vi prøver på nytt.
Er det nå noen av dere som ser en svakhet her?

Klikk

Svakheten jeg vil fram til er at vi bruker default caset for å fange opp tilfellet der ILockSource returnerer Faulted
Problemet jeg har med detter er at koden fanger Faulted caset på en implisitt måte.
For å være sikker på at det er Faulted vi prøver å fange opp, så må jeg lete.
Jeg må åpne ILockSource for å se hva som spyttes ut
Dere spør sikkert dere selv nå "Hvem kunne finne på å skrive koden sin slikt?"
Er det noen av dere som har lyst til å gjette?
Det var Claude Code. Med Fable modellen!!
Men, la oss ikke peke fingeren her. La oss heller prøve å være eksplisitt
-->

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
    ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry"
};
```

<!--

- Klikk
- Klikk

Dette så jo mye bedre ut.
Med å endre underscore til å være Faulted har vi gjort det implisitte eksplisitt.
Nå kommer det tydelig fram at det bare er to caser switchen skal dekke.
- Klikk
Men nå har vi støtt på et annet problem. Hva er det?

-->
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

<!--
Koden kompilerer ikke lengre. Fordi switchen håndterer ikke lengre alle mulige utfall.
- Klikk
Vi er nødt til å legge på en underscore.
-->

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

<!--
Vi velger å kaste en exception i dette tilfellet.
Med det signaliserer vi at denne kodesnutten ikke er designet for flere caser.
-->

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
Men hvorfor tvinger kompilatoren oss til å legge til dette?
Response har jo bare to undertyper.
Hvilke andre utfall kan det være snakk om?
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
Om vi tar en titt på arkitekturen våres igjen. Så ser vi at appen består av 3 dller.

- klikk

Mt.DistinctComics og Mt.Marble har begge referanser til Mt.Domain
Begge disse har lov til å arve fra typer i Mt.Domain
Siden kompilatoren ikke ser utenfor sin egen dll, så vet den ikke om potensielle andre dller som arver.
Derfor tvinger den oss til å ta hensyn til uforutsette typer som skulle dukke opp. 

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

<!--
I Mt.DistinctComics ser vi faktisk at noen har arvet fra ILockSource.Response.
- Klikk
Dette er noe kompilatoren tillater. Men det er helt feil bruk av responsen.
Om utvikleren ønsket en tredje type, så skulle hen ha definert den i Mt.Domain under ILockSource.Response.
Men han var på fest i går og sov dårlig i natt og tenkte seg ikke om. Og dermed havnet vi her.
Men, frykt ikke. Med neste versjon ac c# så kan vi forhindre dette.
Er det noen som tørr å gjette hvilken feature vi skal benytte oss av?
-->
---

# The `closed` newcomer
<v-click>

- Only types in the same assembly can inherit

</v-click>

<v-click>

- Assumes `abstract`

</v-click>

<!--
Vi skal benytte oss av closed hierarchies. Det er et nytt nøkkelord som sier at
- klikk
Kun typer i samme assembly kan arve
- klikk
Typer som er closed vil også være abstract.
-->

---

# The `closed` newcomer
```csharp {all|6}
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
<!--
Så vi endrer
- klikk
abstract nøkkel ordet til Response
-->

---

# The `closed` newcomer
```csharp {6|all}
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

<!--
Til å være closed
- klikk
-->

---

# The `switch` loses the discard
```csharp{all|10}
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

<!--
Om vi nå tar en titt på switchen
- klikk
Så er det ikke lengre behov for default caset. Vi kan fjerne den
- klikk
-->

---

```csharp {none|all}
// Mt.Domain.Handler
var action = lockSource.Handle(migrationId) switch
{
    ILockSource.Response.Locked
        => $"✅ Source locked — advancing migration {migrationId} to Transform",

    ILockSource.Response.Faulted(var reason)
        => $"⏰ Lock faulted ({reason}) — scheduling retry",
};
```

<!--
Fordi kompilatoren er nå trygg på at den kjenner til alle typer som arver fra Response
- klikk
-->

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

<!--
Om vi ser tilbake til adapteret der noen hadde arvet fra ILockSoure.Response
Så vil ikke dette lengre kompilere
- klikk
Akkurat slik vi ønsker det. 

-->

---
layout: center
---

<div class="flex items-center justify-center gap-8">
  <img src="/billymays.jpg" class="h-90 rounded-lg shadow-xl -rotate-2" alt="Billy Mays" />
  <div class="bg-white border-2 border-black rounded-2xl px-6 py-4 text-4xl font-bold shadow-lg rotate-2">
    But wait — there's more!
  </div>
</div>

<!--
Men Microsoft har gitt oss enda en måte å løse dette på.
Hva kan det være?
-->

---

# The union type
- Alternative way to exhaust the switch
- Works on types that have nothing to do with each other

```csharp {all|5}
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
<!--
Union types!
Union types lar en metode returnere flere typer som ikke har noe med hverandre å gjøre
Vi skal nå skrive om ILockSource med å benytte oss av union types.
- klikk
Vi begynner med å endre Response fra å være en abstract record
-->

---

# The union type
- Alternative way to exhaust the switch
- Works on types that have nothing to do with each other

```csharp {5}
public interface ILockSource
{
    Response Handle(long migrationId);

    public union Response
    {
        /// <summary>Source locked the hero.</summary>
        public sealed record Locked : Response;

        /// <summary>Source did not lock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
```
<!--
Til å være en `union`
-->

---

# The union type
- Alternative way to exhaust the switch
- Works on types that have nothing to do with each other

```csharp {5 | all | 8,11}
public interface ILockSource
{
    Response Handle(long migrationId);

    public union Response(Response.Locked, Response.Faulted)
    {
        /// <summary>Source locked the hero.</summary>
        public sealed record Locked : Response;

        /// <summary>Source did not lock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
```

<!--
Videre legger vi in Locked og Faulted som parametre.
Her definerer vi hva unionen består av
-klikk
Til slutt så fjerner vi Response som basetype til Locked og Faulted
-->

---

# The union type
- Alternative way to exhaust the switch
- Works on types that have nothing to do with each other

```csharp {8,11|all}
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
Nå vil ILockSource fungere nøyaktig slik varianten med `closed` fungerte.
Vi trenger ikke endre koden noe annet sted

-klikk

Men dette var jo veldig irriterende. Nå må vi jo ta en beslutning om hvilken metode som er rett. 
Så hva er rett å bruke
-->

---

# When to use `union` and when to use `closed`
- If the types represent different versions of the same thing? => `closed`
- If the types represent completely different things => `union`


<!--
Som en regel så sier vi at closed brukes når typene representerer forskjellige utgaves av samme sak
og når typene representerer to forskjellige ting så bruker vi `union`
-->

---

# Which one is correct?

<div class="grid grid-cols-2 gap-4 text-sm">

<div v-click class="absolute bottom-10 right-10 flex items-start gap-3">
  <div class="bg-white border-2 border-black rounded-2xl px-4 py-2 text-2xl font-bold shadow-lg -rotate-2 self-start">
    That's way too vague!
  </div>
  <img src="/facepalm.jpg" class="h-48 rounded-lg shadow-xl rotate-1" alt="Facepalm statue" />
</div>

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
- If the types represent different versions of the same thing? => `closed`
- If the types represent completely different things => `union`

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
