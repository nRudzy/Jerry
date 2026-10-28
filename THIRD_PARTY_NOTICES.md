# Notices des composants distribués avec Jerry

La distribution Windows inclut le runtime .NET autonome, NAudio,
Whisper.net et ses bibliothèques natives Whisper/ggml, ainsi que deux polices.
Les licences et les métadonnées NuGet de la publication sont conservées dans
`licenses/`. Les fichiers de licences des polices restent dans `Assets/Fonts/`.
`payload-manifest.json` donne l'empreinte SHA-256 de chaque fichier distribué.

| Composant | Version de référence | Licence et provenance |
| --- | --- | --- |
| .NET et Windows Desktop | .NET 10, version exacte dans `licenses/` | [Licences des distributions .NET](https://github.com/dotnet/core/blob/main/license-information.md). Les licences et notices des runtime packs utilisés sont incluses. |
| NAudio et modules Asio/Core/Midi/Wasapi/WinForms/WinMM | 2.2.1 | MIT, Copyright 2020 Mark Heath. [Source](https://github.com/naudio/NAudio/tree/v2.2.1). |
| Whisper.net et Whisper.net.Runtime | 1.9.0 | MIT, Copyright (c) 2024 sandrohanea. [Source](https://github.com/sandrohanea/whisper.net/tree/1.9.0). |
| whisper.cpp et ggml natifs | whisper.cpp 1.8.2, inclus dans Whisper.net.Runtime 1.9.0 | MIT, Copyright (c) 2023-2024 The ggml authors. [Source et licence](https://github.com/ggml-org/whisper.cpp/blob/v1.8.2/LICENSE). |
| Microsoft.Extensions.AI.Abstractions | 10.0.0, dépendance de Whisper.net | MIT, Copyright (c) .NET Foundation and Contributors. [Source](https://github.com/dotnet/extensions). |
| Microsoft Visual C++ Runtime et OpenMP x64 | Version exacte et empreintes dans `licenses/visual-cpp-runtime.json` | Microsoft Software License Terms. DLL officielles CRT et OpenMP issues de `VC/Redist/MSVC`, déployées dans le dossier de l'application. [Règles de redistribution](https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files), [fichiers redistribuables Visual Studio](https://learn.microsoft.com/visualstudio/releases/2022/redistribution), [conditions](https://visualstudio.microsoft.com/license-terms/). |
| Montserrat ExtraBold | 7.200 | SIL Open Font License 1.1. Copyright 2011 The Montserrat Project Authors. Licence complète dans `Assets/Fonts/Montserrat-OFL.txt`. |
| Oswald Bold | 4.100 | SIL Open Font License 1.1. Copyright 2016 The Oswald Project Authors. Licence complète dans `Assets/Fonts/Oswald-OFL.txt`. |

Les DLL Visual C++ restent privées à cette application. Leur mise à jour relève
des futures publications Jerry ; aucune installation système ou élévation
n'est effectuée par Jerry Setup.

FFmpeg et les modèles de transcription ne font pas partie de l'installeur.
Les fonctions correspondantes les téléchargent séparément à la demande dans
les dossiers de données de l'utilisateur. Une mise à jour ou une désinstallation
de Jerry conserve ces fichiers.

## Texte MIT applicable aux composants ci-dessus

Les avis de copyright propres à chaque composant sont indiqués ci-dessus et
dans les licences d'origine incluses. Le texte suivant accompagne notamment
les bibliothèques natives Whisper/ggml :

```text
MIT License

Copyright (c) 2023-2024 The ggml authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Ce même texte de permission et de garantie accompagne
Microsoft.Extensions.AI.Abstractions avec l'avis « Copyright (c) .NET Foundation
and Contributors ». Les licences NAudio et Whisper.net complètes sont également
copiées depuis les packages NuGet restaurés, sans modification.
