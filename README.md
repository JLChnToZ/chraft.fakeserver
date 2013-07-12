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
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="port" value="25568" />
    <add key="motd" value="(MOTD for the server)" />
    <add key="serverVersion" value="(Fake server version)" />
    <add key="onlinePlayers" value="(Fake online players)"/>
    <add key="maxPlayers" value="(Fake maximum players)"/>
    <add key="successWebPage" value="(URL for sending verify code, place {0} for where is player name, {1} for verify code, {2} for IP address)" />
    <add key="tokenInvalidText" value="(Token Invalid message)" />
    <add key="connectionFailText" value="(Failed to connect to minecraft.net message)" />
    <add key="verifyFailText" value="(Non paid player message)" />
    <add key="successText" value="(Verify successful message, place {0} to where you want to display verify code)" />
  </appSettings>
</configuration>
```
Please modify the values above to make it suitable for your needs.
- Run the exe file with .NET Framework or Mono, which is the server itself.
