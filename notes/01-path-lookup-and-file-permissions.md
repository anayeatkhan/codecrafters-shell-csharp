# PATH Lookup & File Permissions in C#

Notes from the `type` command stage. These are the .NET APIs used to find an executable on `PATH`, and what Unix file permissions mean.

---

## 1. The big picture

When you type `type ls`, the shell has to answer: *"Where on disk is the program called `ls`?"*

```
PATH = /usr/bin:/usr/local/bin:/bin
                │
                ▼  split on ':'
   ["/usr/bin", "/usr/local/bin", "/bin"]
                │
                ▼  for each dir, in order
   /usr/bin/ls        → exists? executable?  ✅ → print "ls is /usr/bin/ls", stop
   /usr/local/bin/ls  → (never checked)
```

The steps are:

1. **Read** the `PATH` variable: `Environment.GetEnvironmentVariable`
2. **Split** it into directories: `string.Split` + `Path.PathSeparator`
3. **Build** a full file path: `Path.Combine`
4. **Check** that the file exists: `File.Exists`
5. **Check** that it's executable: `File.GetUnixFileMode`

---

## 2. `Environment.GetEnvironmentVariable`

**Environment variables** are key/value strings that the OS gives every process when it starts. `PATH`, `HOME` and `USER` are common ones. In a terminal:

```bash
echo $PATH        # Linux/macOS
echo $env:PATH    # PowerShell
```

In C#:

```csharp
string? path = Environment.GetEnvironmentVariable("PATH");
```

| Detail | Meaning |
|---|---|
| Namespace | `System` (always available) |
| Returns | `string?`: the value, or **`null` if the variable isn't set** |
| Case | Linux: case-sensitive (`PATH` ≠ `Path`). Windows: case-insensitive |

Because it can return `null`, your code used two null operators:

```csharp
Environment.GetEnvironmentVariable("PATH")?.Split(...) ?? Array.Empty<string>();
//                                         ^^                ^^
//            ?.  "if not null, call Split; otherwise the result is null"
//            ??  "if the left side is null, use the right side instead"
```

So if `PATH` is missing, you get an empty array instead of a crash.

> The tester launches your program with `PATH="/usr/bin:/usr/local/bin:$PATH" ./your_program.sh`. That sets `PATH` **for your process only**, and your program reads it with this call.

---

## 3. `string.Split`

`Split` breaks one string into an array of pieces wherever a separator appears.

```csharp
"a:b:c".Split(':')                // ["a", "b", "c"]
"a::c".Split(':')                 // ["a", "", "c"]   ← empty piece between the two ':'
"a::c".Split(':', StringSplitOptions.RemoveEmptyEntries)  // ["a", "c"]
```

### `StringSplitOptions`

| Option | Effect |
|---|---|
| `None` | Keep everything, including empty strings |
| `RemoveEmptyEntries` | Drop `""` pieces |
| `TrimEntries` | Trim whitespace around each piece (.NET 5+) |

You can combine them: `StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries`.

**Why remove empty entries for PATH?** A PATH like `/usr/bin::/bin` or one ending in `:` produces `""`, and `Path.Combine("", "ls")` returns just `"ls"`, a relative path that looks in the *current directory*. Real shells do treat an empty entry as "current directory", but for this challenge, skipping it is safer.

---

## 4. The `Path` class

`System.IO.Path` is a **static helper class for working with path strings**. It never touches the disk; it only manipulates text.

### Separators: two different things

| Property | Linux/macOS | Windows | Used for |
|---|---|---|---|
| `Path.PathSeparator` | `':'` | `';'` | Between **entries in PATH** |
| `Path.DirectorySeparatorChar` | `'/'` | `'\\'` | Between **folders in one path** |

```
Linux:   PATH = /usr/bin:/usr/local/bin
                        ^ PathSeparator
                ^   ^ DirectorySeparatorChar

Windows: PATH = C:\Windows;C:\Tools
                          ^ PathSeparator
                  ^ DirectorySeparatorChar
```

This is why hardcoding `Split(';')` broke on Linux. **Always use `Path.PathSeparator`.**

### `Path.Combine`

`Path.Combine` joins pieces of a path and inserts the correct directory separator for you:

```csharp
Path.Combine("/usr/bin", "ls")         // "/usr/bin/ls"
Path.Combine("/usr/bin/", "ls")        // "/usr/bin/ls"   (doesn't double the '/')
Path.Combine("C:\\Tools", "git.exe")   // "C:\Tools\git.exe"   (on Windows)
Path.Combine("a", "b", "c")            // "a/b/c"
```

Gotchas:

```csharp
Path.Combine("", "ls")            // "ls"        → empty parts are ignored
Path.Combine("/usr/bin", "/bin/ls") // "/bin/ls" → a rooted 2nd part THROWS AWAY the 1st!
Path.Combine(null, "ls")          // ArgumentNullException
```

Why not just write `dir + "/" + name`? Because then you'd have to handle trailing slashes and the different separator on each OS yourself.

### Other handy `Path` methods

```csharp
Path.GetFileName("/usr/bin/ls")       // "ls"
Path.GetDirectoryName("/usr/bin/ls")  // "/usr/bin"
Path.GetExtension("app.exe")          // ".exe"
Path.GetFullPath("ls")                // absolute path relative to current directory
```

---

## 5. The `File` class

`System.IO.File` is a **static class for working with files on disk**. Unlike `Path`, it *does* touch the filesystem. "Static" means you never write `new File()`; you call methods directly, like `File.Exists(...)`.

```csharp
File.Exists(path)          // bool: is there a FILE at this path?
File.ReadAllText(path)     // string: whole file contents
File.WriteAllText(path, s) // create/overwrite a file
File.Delete(path)
File.GetUnixFileMode(path) // permission bits (see below)
```

### `File.Exists` details

- Returns `false` (it **never throws**) if the path is null or invalid, the file doesn't exist, **the directory doesn't exist**, or you lack permission to look.
- Returns `false` for **directories**. Use `Directory.Exists` for those.

That's why it's safe to call on PATH entries that point to missing folders.

### Related classes

| Class | Style | Use when |
|---|---|---|
| `File` | static methods, take a path string | one-off operations |
| `FileInfo` | object, `new FileInfo(path)` | you'll query the same file many times (`.Exists`, `.Length`, `.UnixFileMode`) |
| `Directory` / `DirectoryInfo` | same idea, for folders | listing or checking directories |

---

## 6. Unix file permissions (the concept)

Every file on Linux/macOS stores **9 permission bits** in 3 groups of 3:

```
 -  rwx  r-x  r-x    your_program.sh
 │  └┬┘  └┬┘  └┬┘
 │   │    │    └── Other: everyone else
 │   │    └─────── Group: users in the file's group
 │   └──────────── User:  the file's owner
 └──────────────── file type: '-' file, 'd' directory, 'l' symlink
```

Each group has three permissions:

| Letter | Name | On a file | On a directory |
|---|---|---|---|
| `r` | read | read the contents | list the files inside |
| `w` | write | modify the contents | create/delete files inside |
| `x` | execute | **run it as a program** | enter it (`cd`) / access files inside |
| `-` | — | permission not granted | |

You can see them with `ls -l`:

```bash
$ ls -l /usr/bin/ls
-rwxr-xr-x 1 root root 142312 ... /usr/bin/ls
```

That means the owner (root) can read, write and execute it, and everyone else can read and execute it.

### Octal notation (`chmod 755`)

Each `rwx` group is a 3-bit number: `r = 4`, `w = 2`, `x = 1`. Add them up per group:

| Octal | Bits | Meaning |
|---|---|---|
| `7` | `rwx` | 4+2+1 |
| `6` | `rw-` | 4+2 |
| `5` | `r-x` | 4+1 |
| `4` | `r--` | 4 |
| `0` | `---` | nothing |

```bash
chmod 755 script.sh   # rwxr-xr-x : typical program
chmod 644 notes.txt   # rw-r--r-- : typical text file (NOT executable)
chmod +x script.sh    # add x for everyone
chmod -x script.sh    # remove x → this is how the tester makes "file exists but not executable"
```

### Special bits (just so you recognise them)

| Bit | Name | Effect |
|---|---|---|
| SetUser (setuid) | `s` in user-x spot | program runs as the file's owner (e.g. `passwd` runs as root) |
| SetGroup (setgid) | `s` in group-x spot | runs as the file's group |
| Sticky | `t` in other-x spot | on dirs like `/tmp`: only the owner can delete their files |

---

## 7. `File.GetUnixFileMode` and the `UnixFileMode` enum

```csharp
UnixFileMode mode = File.GetUnixFileMode(path);   // .NET 7+
```

This returns the permission bits above as a **`[Flags]` enum**: one number where each bit is a separate yes/no flag.

| `UnixFileMode` member | Value | Octal | `ls -l` position |
|---|---|---|---|
| `None` | 0 | 0000 | |
| `OtherExecute` | 1 | 0001 | `-------- x` |
| `OtherWrite` | 2 | 0002 | `------- w-` |
| `OtherRead` | 4 | 0004 | `------ r--` |
| `GroupExecute` | 8 | 0010 | `----- x---` |
| `GroupWrite` | 16 | 0020 | `---- w----` |
| `GroupRead` | 32 | 0040 | `--- r-----` |
| `UserExecute` | 64 | 0100 | `-- x------` |
| `UserWrite` | 128 | 0200 | `- w-------` |
| `UserRead` | 256 | 0400 | `r--------` |
| `StickyBit` | 512 | 1000 | |
| `SetGroup` | 1024 | 2000 | |
| `SetUser` | 2048 | 4000 | |

A file with `rwxr-xr-x` (755) returns `UserRead | UserWrite | UserExecute | GroupRead | GroupExecute | OtherRead | OtherExecute` = 256+128+64+32+8+4+1 = **493** (which is 755 in octal).

### Reading flags with bitwise operators

```csharp
// |  (OR)  → combine flags into a mask
var anyExec = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
//            binary 001 000 000        | 000 001 000                | 000 000 001  = 001 001 001

// &  (AND) → keep only the bits that are in BOTH
bool isExec = (mode & anyExec) != 0;   // true if ANY of the three x bits is set
```

Worked example:

```
mode     = rw- r-- r--  = 110 100 100
anyExec  = --x --x --x  = 001 001 001
mode & anyExec          = 000 000 000  → 0 → NOT executable

mode     = rwx r-x r-x  = 111 101 101
mode & anyExec          = 001 001 001  → not 0 → executable ✅
```

An easier-to-read alternative is `mode.HasFlag(UnixFileMode.UserExecute)`. It checks one flag at a time.

### When it throws

| Situation | Exception |
|---|---|
| File doesn't exist | `FileNotFoundException` |
| Running on **Windows** | `PlatformNotSupportedException` |

That's why the order matters: **check `File.Exists` first, then read the mode.**

> **Simplification note:** "Any x bit is set" isn't quite what a real shell checks. A real shell asks "can *I*, this user, execute it?", which is the `access(path, X_OK)` system call. For example, if only `UserExecute` is set and you aren't the owner, you can't run it. The "any x bit" check is good enough for the tester.

### Why Windows is different

Windows has no execute bits. It decides what's runnable by **file extension**, using the `PATHEXT` variable (`.EXE;.BAT;.CMD;...`). That's why `git` on Windows is really `git.exe`, and why Linux programs like `/usr/bin/ls` have no extension at all.

---

## 8. Putting it all together

```csharp
static string? FindExecutable(string name)
{
    // 1–2. Read PATH and split it with the OS-correct separator
    string[] dirs = Environment.GetEnvironmentVariable("PATH")?
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        ?? Array.Empty<string>();

    const UnixFileMode anyExec =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    foreach (var dir in dirs)
    {
        // 3. Build "/usr/bin" + "ls" → "/usr/bin/ls"
        string fullPath = Path.Combine(dir, name);

        // 4. Missing file or folder → just try the next directory
        if (!File.Exists(fullPath)) continue;

        // 5. Exists → is any execute bit set?
        if (OperatingSystem.IsWindows()) return fullPath;   // no x bits on Windows
        if ((File.GetUnixFileMode(fullPath) & anyExec) != 0) return fullPath;

        // exists but not executable → keep searching
    }
    return null; // not found anywhere
}
```

---

## Quick reference

| Need | API | Namespace |
|---|---|---|
| Read an env var | `Environment.GetEnvironmentVariable("PATH")` | `System` |
| PATH entry separator | `Path.PathSeparator` (`:` / `;`) | `System.IO` |
| Folder separator | `Path.DirectorySeparatorChar` (`/` / `\`) | `System.IO` |
| Split, skip blanks | `s.Split(sep, StringSplitOptions.RemoveEmptyEntries)` | `System` |
| Join path parts | `Path.Combine(dir, name)` | `System.IO` |
| File exists? | `File.Exists(path)` (never throws) | `System.IO` |
| Folder exists? | `Directory.Exists(path)` | `System.IO` |
| Permission bits | `File.GetUnixFileMode(path)` (.NET 7+, Unix only) | `System.IO` |
| Test a flag | `(mode & flag) != 0` or `mode.HasFlag(flag)` | |
| Which OS? | `OperatingSystem.IsWindows()` / `IsLinux()` | `System` |
