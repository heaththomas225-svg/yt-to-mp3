// (re)writes cross/Purphex.Core/Purphex.Core.csproj from the Windows SRC list: every file without Windows-only code, plus the ones named here
const fs = require('fs'); const D = require('path').join(__dirname, '..', '..') + '/';
const env = fs.readFileSync(D + '_env.sh', 'utf8'); const src = env.match(/SRC="([^"]*)"/)[1].split(/\s+/).filter(Boolean);
const force = (process.argv[2] || '').split(',').filter(Boolean); const skip = (process.argv[3] || '').split(',').filter(Boolean);
const bad = /System\.Windows\.Forms|DllImport|System\.Drawing|Microsoft\.Win32|ProtectedData|WebView2|StudioForm/;
const files = src.filter((f) => f !== 'AssemblyInfo.cs' && f !== 'Audio.WaveOut.cs' && !skip.includes(f) && (force.includes(f) || !bad.test(fs.readFileSync(D + f, 'utf8'))));
const extra = fs.existsSync(D + 'cross/Purphex.Core/Platform') ? fs.readdirSync(D + 'cross/Purphex.Core/Platform').filter((f) => f.endsWith('.cs')).map((f) => 'Platform/' + f) : [];
const xml = `<Project Sdk="Microsoft.NET.Sdk">
  <!-- Purphex core: the audio engine, DSP, plugins and sound library for Windows, macOS and Linux (.NET 8).
       The .cs files are the same ones the Windows app builds (linked from the repo root); Platform/ holds the cross-platform stand-ins. -->
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0162;CS0168;CS0169;CS0219;CS0414;CS0649;CS0618;CS0612;CS1998;CA1416;SYSLIB0011;SYSLIB0014;CS8981;CS0108;CS0114</NoWarn>
    <DefineConstants>$(DefineConstants);PURPHEX_CORE</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Purphex.Smoke" />
    <InternalsVisibleTo Include="Purphex.Desktop" />
    <InternalsVisibleTo Include="Purphex.Platform" />
  </ItemGroup>
  <ItemGroup>
${files.map((f) => `    <Compile Include="../../${f}" />`).join('\n')}
${extra.map((f) => `    <Compile Include="${f}" />`).join('\n')}
  </ItemGroup>
</Project>
`;
fs.writeFileSync(D + 'cross/Purphex.Core/Purphex.Core.csproj', xml); console.log(files.length + ' shared files, ' + extra.length + ' platform files');
