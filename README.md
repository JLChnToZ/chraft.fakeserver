chraft.fakeserver
=================

Minecraft Paid User Verify Service.

This is a standalone verify server, which is used to verify the paid IDs in Minecraft.

## Instruction for players
- Select the multiplayer mode
- Click Direct Connect
- Enter the IP or domain name that hosted this verify server
- Click Connect
- Wait for a while, it will return the verify code to you if you are paid user
- Follow the instruction where you need to verify to

## Instruction for developers
- Compile the program with Mono or .NET Framework or download it.
- Make a new XML file named ChraftMCPaidVerify.config.xml with following contents:
```xml
&lt;?xml version="1.0" encoding="utf-8" ?&gt;
&lt;configuration&gt;
  &lt;appSettings&gt;
    &lt;add key="port" value="25568" /&gt;
    &lt;add key="motd" value="§6Verify Paid User §7(Non play-able)" /&gt;
    &lt;add key="serverVersion" value="1.4.7" /&gt;
    &lt;add key="onlinePlayers" value="0"/&gt;
    &lt;add key="maxPlayers" value="1"/&gt;
    &lt;add key="successWebPage" value="http://example.com/verify.php?user={0}&amp;code={1}" /&gt;
    &lt;add key="tokenInvalidText" value="§c金幣不正確." /&gt;
    &lt;add key="connectionFailText" value="§c無法從伺服器取得驗證, 可能伺服器在維護中或是網絡問題, 請稍後再試." /&gt;
    &lt;add key="verifyFailText" value="§c請支持正版, 謝謝." /&gt;
    &lt;add key="successText" value="§a驗證成功. 您的驗證碼為§6§l{0}§r§a." /&gt;
  &lt;/appSettings&gt;
&lt;/configuration&gt;
```
Please modify the values above to make it suitable for your needs.
- Run the exe file with .NET Framework or Mono, which is the server itself.
