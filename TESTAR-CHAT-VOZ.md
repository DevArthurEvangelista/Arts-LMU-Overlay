# Teste do chat por voz — 0.12.5 (experimental)

Este é um pacote de código-fonte para compilar no Windows; **não contém um EXE 0.12.5 validado**.

1. Compile com `COMPILAR.cmd` ou `dotnet build LMUOverlay.csproj -c Release`. O Windows precisa ter reconhecimento de fala em português instalado e microfone padrão funcional. Se a compilação reclamar de `System.Speech`, instale o .NET Framework 4.8 Developer Pack.
2. No LMU, configure a função **Realtime Chat** para **Enter**. Abra o jogo normalmente pela Steam com o EAC para jogar online. Feche o campo de chat antes de testar.
3. No Arts, vá em **Atalhos > Falar no chat do LMU**, vincule um botão livre do volante e ative **envio automático**.
4. Em uma sessão privada de teste ou treino com chat permitido, mantenha o LMU em primeiro plano, segure o botão, fale uma frase curta em português e solte. O Arts tentará abrir o chat, digitar a transcrição e enviá-la.

A função foi preparada para teste, mas não foi compilada nem verificada neste ambiente Windows/LMU. Não foi possível confirmar se o LMU aceita `SendInput` Unicode no campo de chat; se não aceitar, precisaremos ajustar o método de inserção. O app não injeta DLL no jogo e não mexe no EAC. A função não verifica as regras de chat de cada evento. Respeite as regras da sessão em que estiver jogando.

**Feedback útil:** versão do Windows, idioma do reconhecimento instalado, se o botão foi reconhecido, se o chat abriu, se o texto apareceu, se Enter enviou e se acentos foram aceitos. Se a configuração do chat já estiver aberta antes de soltar, feche-a e tente de novo.
