# Voices and Names

A companion to `spec-entropy-cube/lore-and-tone.md` and `faces-and-races.md` (CAP-32), from story 4.4. The audience is *Ocarina of Time*'s: pre-teens to adults. Everything stays campy and lighthearted.

**Code:** the voices live in `Npcs/Dialogue.cs` (per-voice flavour, roles, patron and mechanic lines) and in `HintGenerator.VoiceTable` (hint openers and connectors). The names live in `Npcs/NpcNames.cs`. `VoicesAndNamesTests` checks both.

## The rules every voice keeps

- **Subtle science:** no real scientist's name and no science word. `InWorldText` is the one banned list, and the shared scan covers every voice.
- **Names and stars:** regions and items keep their trailing `*`. Characters have chosen names, so they never carry one.
- **Hints keep their facts:** a voice changes only the opener, the connector before a gate's look, the remark after a vague gate hint, the sealed-face tail and the sentence end. The region, screen, item nickname, gate look and town edge are worded the same in every voice, so every hint stays true.
- **Length:** a line is at most 200 characters (`Dialogue.MaxLineLength`), which is three lines of the dialogue box.

## The voices

There is one voice per race. The dinosaurs have two: ground and avian.

| Voice | Who | How they talk | Every line has (tested) | Example |
|---|---|---|---|---|
| Town | Townsfolk | A friendly, campy, gossipy mix | (no marker) | "Howdy, neighbour! Lovely mess today, isn't it?" |
| Finch | Biology finches | Near-Shakespearean and proper, but readable | an archaic form: thee, thou, thy, prithee, 'tis, doth, hath, verily, forsooth, hark, good morrow... (not "mine" or "pray") | "Hast thou heard? The thin-beaks think themselves above us all." |
| Mushroom | Chemistry folk | Emotionally reactive: they flare up, fizz, calm down and bond quickly. Emphasis goes in \*stars\*; the only capitals-only shouts are CLANG and BOOM (RAWR and SKRAW for the dinosaurs). | an emotional word or swing (oh, ooh, ugh, argh, sob, sniff, gasp, eek, wow, love, happy, furious, thrilled, sad, bored...); a "!" alone is not enough | "Buy my spores! Or don't! See if I care! ...I care so much." |
| Alien | Physics folk | Philosophical and abstract: they muse on time, weight and motion | a musing word (perhaps, wonder, ponder, muse, consider, truly, ask, thought...); a "?" alone is not enough | "Today I sell floaty socks. Tomorrow, perhaps, the floaty socks sell me." |
| Shape | Math shape people | Logical, terse and a little cool, but helpful; they reason in if/then steps | **no "!"**, plus if / then / therefore / thus / hence / given | "These are slightly used angles. If they are slightly used, then they are slightly cheaper." |
| Ground | Terrestrial dinosaurs, and their leader Chief Rockjaw | Rugged and outdoorsy: practical, a bit gruff | an outdoorsy word: trail, camp, ground, rock, cliff, ridge, mud, hill, canyon, storm, dirt, bone, scout, boot... | "Got trail rations and old bones. Got mud on 'em, too. Mud's free." |
| Sky | Avian dinosaurs | Outdoorsy, but breezier | an outdoorsy word: sky, cloud, wind, breeze, rain, sun, snow, feather... | "Got feathers, lightly windblown. Caught 'em on the wind this morning." |

**Hint wording per voice** (full-density openers; the sparse ones are in the same register):
- **Town:** "They say", "Word around the ruins is", "A little bird told me".
- **Finch:** "Prithee, hark: 'tis said", "Verily, I have heard that", "Good morrow! Mine ears tell me".
- **Mushroom:** "Ooh! Ooh! I heard", "Gasp! Guess what:", "Ugh, FINE, I'll tell you:". These hints end in "!".
- **Alien:** "I wonder... could it be that", "Consider, if you will:", "Perhaps, in some deep sense,". The musing lives in the opener only; the question opener ends its hint with "?", the rest with ".".
- **Shape:** "If my figures hold, then", "Given the facts, it follows that", "Logically, therefore,".
- **Ground:** "Word on the trail is", "Scouted it myself:", "Back at camp they say".
- **Sky:** "Word on the wind is", "From up in the sky, I'd say", "The breeze tells me".

**Who speaks in which voice:**
- An NPC speaks in its own race's voice wherever it stands, including the Town's visitors from each face.
- A dinosaur speaks by its kind: avian dinosaurs use Sky, the others Ground.
- Townsfolk, the greeter and Sage Conkwell speak in the Town voice.

**Leaders (4.5):** a leader greets the player (`Dialogue.LeaderLines`), then gives the region's quest hook in their voice (`Dialogue.QuestLines`): the region is coming apart, so bring back what was lost and help mend it. Story 4.8 fills the hook in with the face's fragments. Chief Rockjaw's hook suits both dinosaur voices.

**Shop keepers (4.6):** a shop's keeper (`Dialogue.KeeperLines`, one pair per voice) welcomes the player to a bare counter, then says Jumbles* (`InWorldText.Currency`) are spent between trips (spending stays on the between-runs screen). Their label reads "Name the <race> shopkeeper". Everyone else inside a building talks like the town's residents.

## Names

- **Make them punny but subtle.** A name may hint at its culture (a corner, a hedgerow, a fizz, a musing, a trail, a breeze), never at a real scientist.
- **Generic NPCs** draw a seeded name from their voice's pool. The pool is shuffled by (seed, voice) on its own RNG stream, `RngStreams.NpcNames` (22), so naming never shifts parts, spots or wander. Within one population no two people share a name.
- **More people than names:** once a population outgrows its pool, later people carry a numeral ("Bessie II"), so names stay unique (a 5x5 Town does this).
- **The speaker label** reads "Name the kind", for example "Pipwick the finch merchant" or "Queen Wrennifer the finch leader". The greeter reads "Hattie Hullo the greeter".
- **Markers never come from names:** the voice tests strip character names, item names and nicknames, and region names before looking for a voice's markers, and the markers are whole words.
- **The text rule** reads a character's name as chosen only where the whole name stands ("Queen Wrennifer", "Primsworth the Demon", "Pipwick II"); a lone "Queen" or "Old" still needs its star.

| Pool | Names |
|---|---|
| Town | Bessie, Wilbur, Mabel, Ned, Gertie, Barnaby, Clem, Rosie, Alfie, Mungo, Peggy, Ozzie, Winnie, Bert, Dottie, Fennel, Mopsy, Rufus, Tilly, Percy, Nell, Hobb, Lulu, Jasper |
| Finch | Pipwick, Wrenna, Featherby, Nestor, Quillon, Fenwick, Linnet, Chirpwell, Tansy, Sparrowby, Robina, Twiggs, Peregrine, Thistle, Warbleton, Hedgely, Larkspur, Dunnock, Seedwell, Plumley |
| Mushroom | Fizz, Sizzle, Morel, Truffle, Portia, Enoki, Popkin, Flarebee, Inky, Chanty, Puffkin, Bubbla, Cappy, Sporrow, Shimmer, Gillian, Russ, Fumi, Blewitt, Crackle |
| Alien | Ponderel, Musewell, Yonder, Lingerby, Driftly, Whenna, Thinkle, Wistrom, Quizzle, Tarrix, Dawdlix, Glimwhen, Eona, Hmmble, Quandra, Wonderly, Murmix, Ruminel, Vastra, Someday |
| Shape | Angela, Trixie, Rhona, Hexter, Octavia, Isolde, Primrose, Dot, Otto, Quincy, Evan, Cubert, Arc, Lina, Rex Tangle, Tess, Polly, Ova, Cornelia, Penny |
| Ground | Flint, Shale, Cragg, Rumble, Granite Gert, Clay, Dusty, Ridge, Cliff, Mossback, Tusk, Slate, Talus, Ruckus, Burl, Boulder Bea |
| Sky | Gale, Breezy, Skye, Cirra, Gusty, Nimbo, Drizzle, Squall, Flurry, Mistral, Sunny, Cumulina, Stormy, Windy Wendy, Puffcloud, Zephyrine |

**Fixed names** (they replace the 1.22 `*` placeholders):

| Who | Name | Was |
|---|---|---|
| Town leader | Mayor Muddleton | (4.3: "Townsperson leader") |
| Finch leader | Queen Wrennifer | (4.3) |
| Mushroom leader | Duchess Flarella | (4.3) |
| Alien leader | Grand Muser Yondrix | (4.3) |
| Shape leader | Chancellor Quadrilla | (4.3) |
| Dinosaur leader (always a ground dinosaur: look, voice and label) | Chief Rockjaw | (4.3) |
| Finch patron | Grandsire Beakwell | Old Fledgling* |
| Mushroom patron | Mother Glimmercap | Glowmother* |
| Alien patron | Old Whenever | Timekeeper* |
| Shape patron | Madam Spanwell | Bridgekeeper* |
| Ground dinosaur patron | Granny Trowel | Old Digger* |
| Sky dinosaur patron | Auntie Cloudeye | Sky-Watcher* |
| The old half-skeleton mushroom | Grandpa Brittlecap | Marcel* |
| The apple-bonked sage | Sage Conkwell | apple-bonked sage* |
| The Town greeter | Hattie Hullo | (a townsperson) |
| The boss | Primsworth the Demon | the Demon* |
