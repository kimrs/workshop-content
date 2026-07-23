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
class: text-white
---

<img src="/heroes-clash.png" class="absolute inset-0 w-full h-full object-cover" />
<div class="absolute inset-0 bg-gradient-to-r from-black/70 to-black/20"></div>

<div class="relative z-10">

# Migrate Distinct Comics heroes to Marble
- Marble has bought Distinct Comics 
- Our job is to migrate heroes from Distinct Comics to Marble

</div>

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
Her er kodesnutten dere så tidligere igjen.
Den viser ILockSource slik den er definert i Mt.Domain.
Den returnerer to typer svar.

- Locked om det eksterne systemet klarte å låse

- Faulted om det eksterne systemet misslyktes.

Vi anser Faulted som et likeverdig forretningscase fordi den representerer ikke noe som gikk galt hos oss, men noe som gikk galt 
i det eksterne systemet.

Under ser vi et utdrag fra Mt.Domain.Handler der vi benytter oss av interfacet.
Den inneholder en switch som skedulerer en retry om systemet ikke ble låst
og ellers avanserer til neste steg. 

Med konteksten ferskt i minnet så ønsker jeg å spørre dere igjen
"Er det noen svakheter med kodesnutten dere ser her?"

- Vent på svar

Det jeg vil fram til er default caset i switchen. 

- Klikk

Istedenfor å eksplisitt konstatere at Locked avanserer videre, så sier vi det implisitt med å bruke default caset.
Problemet med implisitt kode er at jeg må lete for å se hele bildet. I dette tilfellet må jeg ta en titt inn i ILockSource for å
se hva mulighetene er.
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
Med å endre underscore til å være Faulted har vi gjort det implisitte eksplisitt.

- Klikk

Dette så jo mye bedre ut.
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

```csharp {5|all|8,11}
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
Da vil jeg invitere dere til å ta fram telefonene deres og scanne QR koden.
Dere skal stemme på hvilken versjon dere syns ser mest rett ut. Jeg loggfører ikke hvem som
har svart hva, så svar det dere tror uten å se på hva de andre svarer. 
-->

---

# The verdict
- If the types represent different versions of the same thing? => `closed`
- If the types represent completely different things => `union`

<div class="flex justify-center">
  <Poll />
</div>

<div v-click class="absolute bottom-10 right-10 flex items-start gap-3">
  <div class="bg-white border-2 border-black rounded-2xl px-4 py-2 text-2xl font-bold shadow-lg -rotate-2 self-start">
    The rules are too vague!
  </div>
  <img src="/facepalm.jpg" class="h-48 rounded-lg shadow-xl rotate-1" alt="Facepalm statue" />
</div>

<!--
Det ser ut til at dere har svart litt forskjellig her. Det tyder bare på en ting

- Klikk

Reglene er for vage! La oss se om vi kan komme med noen andre regler med å ta en titt på en annen forskjell

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
Når vi bruker `closed` så typene mulighet for å dele data. 

Vi ser her hvordan INotifyCompletion er definert.
Forskjellen på denne kontra ILockSource er at INotifyCompletion returnerer ikke en `Response`
men den tar inn en `Request`. Typen bestemmer hva vi forteller.
Ble superhelten migrert så sender vi med `Migrated`
Om vi kansellerte migreringen fordi datagrunnlaget ikke stemte så sender vi `Cancelled`
Begge disse typene deler Id, så adapteret trenger ikke å convertere typen for å vite Id.
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

<!--
En `union` ikke støtter delt data mellom typene.
Det vil ikke kompilere. Derfor vil det ikke være mulig å velge
`union` for akkurat denne requesten.
Og siden `Request` og `Response` er to sider av samme sak. Så er det ikke naturlig å velge union for `Response` heller.
Det er sansynlig at vi vil møte `Response` som deler data. Så da er det best at vi holder oss konsekvente.
-->
---

# When to use `union` and when to use `closed`
<v-click>

- House style matters

</v-click>

<v-click>

- If the types share data? => `closed`

</v-click>

<v-click>

- If the types does not share data? => `union`

</v-click>


<div v-click class="absolute bottom-10 right-10 flex items-start gap-3">
  <div class="bg-white border-2 border-black rounded-2xl px-4 py-2 text-2xl font-bold shadow-lg -rotate-2 self-start">
    Show them an example with <code>union</code>!
  </div>
  <img src="/madstorgersen.jpg" class="h-48 rounded-lg shadow-xl rotate-2" alt="Mads Torgersen mid-proclamation on stage" />
</div>

<!--
- Klikk

Sagt på en annen måte, så er det husets regler som gjelder

- Klikk

Om ikke huset har etablert noe mønster. Så bruker vi `closed` om typen deler data. Eventuelt om klassen har søsken som gjør det.
Response og Request definert i portene våre anser jeg som søsken.

- Klikk

Om det ikke er tilfellet. Så bruker vi `union`

- Klikk

Før denne presentasjonen så ba Mads Torgersen meg om å vise et eksempel der det gir mening å bruke `union`.
Selvfølgelig vil jeg det. Jeg ville født barna hans om naturen tillot
-->

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

<!--
I det neste eksemplet vil jeg skrive om `Result` pattern til å bruke unions. 
`Result` er noe teamet mitt har blitt svært glade i.
Istedenfor å returnere en `Response` så returnerer vi en `Result` som wrapper `Response`
`Result` kan enten være av typen `Completed` eller den kan være av typen `Failed`
Om noe uforutsett gikk galt i adapteret, så vil vi motta en `Failed` istedenfor `Completed`
Vi bruker altså ikke `Failed` for å representere irriterende foretningstilfeller der eksterne
systemer sliter med å levere. Men heller for å signalisere at noe gikk galt på vår side.
-->
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

<!--
`Result` har en `Then` metode som lar oss chaine uttrykk. Om `Then` returner `Failed`
så vill the neste `Then` utrykkene hoppes over og variabelen vil ende opp med typen `Failed`.
-->

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

<!--
Her ser vi implementasjonen av `Result`
Slik den ser ut nå er den nokså tung å lese. Den er rotete fordi mye av teksten vi ser kun er der
fordi C# krever det.
-->
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

<!--
Fra det vi har lært til nå burde vi ikke trenge noe default case i switchen
-->

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

<!--
Failed inneholder ikke noen underliggende verdi. Så det er ikke noe poeng i å gi den type parametre. Den er der kun fordi
kompilatoren påkrever det. 

-->

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
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes, pointless new
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

<!--
Det er også meningsløst å opprette en ny `Failed` her. Vi gjør det bare fordi
resultatet som forventes er en `Failed` av `TRes` ikke `T`. 
-->

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
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes, pointless new
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

<!--
De unødvendige type parameterene går igjen i definisjonen av `Failed`
-->
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
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes, pointless new
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```
<!--
Når vi nå skriver dette om så starter vi med `Result`

- klikk

Vi endrer den fra å være `abstract record`

-->

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
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes, pointless new
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

<!--
Til å bli en `union`
- klikk
Vi kan da fjerne `Result` som base type for `Completed` og `Failed`
-->
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
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes, pointless new
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```

<!--
Nå som `Failed` ikke har noe med `Result` å gjøre
- klikk
Så kan vi fjerne type parameteren
-->
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
                Failed<T>(var reason) => new Failed<TRes>(reason),  // pointless T and TRes, pointless new
                _ => throw new Exception()                          // pointless default case
            };
    }
}
```
<!--
Videre i switchen
- klikk
Så ser vi den virkelige gulrota. Ikke bare kan vi kvitte oss med typeparameterene.
Vi trenger ikke en gang å newe opp en ny `Failed`.
Det holder å returnere den som allerede er der. 
-->

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

<!--
Til slutt så kan vi kvitte oss med default caset
- klikk
-->

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
Er det ikke vakkert?

- klikk
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

 - Make it harder for developers/claude to missuse code
</v-click>

<!--
I denne AI tiden vi befinner oss i så tar jeg meg ofte i å stille spørsmålet.
Er dette egentilg så viktig?
Enn så lenge så heller jeg mot at det for skalerbare systemer er viktigere enn noen gang å holde koden ryddig.
Vi tilbringer mindre tid med koden når AI skriver den. Det gjør det lettere å glemme hva koden gjør.
Et begrep jeg ofte har hørt i år er "kognitiv gjeld". Altså, problemer med at utviklerne i prosjektet ikke helt
forstår hvordan systemet fungerer. Koden er ikke bare det som får noe til å fungere. Det er også den eneste
100% korrekte instruksjonsmanualen vi har for systemet. Med å fokusere på å holde koden vår fritt for unødvendig
rot gjør vi det enklere for oss selv å forstå hva vi har skapt. Og kan med det, enklere forklare hvorfor noe ikke
gikk som det skulle om uhellet skulle være ute.

Det er ikke bare jeg som syns det fortsatt er viktig. Microsoft hadde ikke fortsatt puttet resurrser i å videreutvikle
C# om det ikke var fordi de fortsatt trudde det hadde en verdi.

Den dagen de også bestemmer seg for at ryddig kode ikke har noen verdi, blir den samme dagen jeg river ned
Mads Torgersen plakaten jeg har hengende på soverommet og erstatter den med noe AI generert.
-->

---
layout: center
---

# Thank you

<div class="flex items-center justify-center gap-12">

<div>

**Everything from today — code, demos, this deck:**
`github.com/kimrs/fagkveld-knirkefritt

- .NET **11 preview 6** · `<LangVersion>preview</LangVersion>` · syntax may shift before GA
- The union & closed-hierarchy design: `github.com/dotnet/csharplang`

</div>

<FeedbackQr url="https://forms.gle/REPLACE_WITH_REAL_FORM_ID" caption="Scan to leave feedback" />

</div>

<!--
Med det så ønsker jeg å takke for meg.

Jeg har brukt .Net 11 preview 6 for å lage kode eksemplene. Syntaksen kan endre seg før den slippes i
november. Presentasjonen kan dere se på github. Som kilde har jeg tatt en titt på forslagene i dotnet
sitt Github repo. 

Før vi går i gang med spørsmål så ønsker jeg at dere gjør meg
en tjeneste. Jeg ønsker å bli bedre på å lage og holde presentasjoner så jeg hadde satt stor pris på 
om dere scannet QR koden og fylte ut tilbakemeldings sjemaet. Jeg loggfører ikke epost adresser, så ikke
bekymre dere for at jeg ser hvem som skriver hva.
-->
