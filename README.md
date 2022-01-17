# VerifyAll
Verification web client that allows automated semantic requirement analysis and formal verification of requirements against C and C++ source codes.
Does not work standalone, needs verification server that contains formal methods tools.

Automated formal verification process and formal tools:

![Automated formal verification process and formal tools](https://github.com/tkratochvila/VerifyAll/blob/main/WebApp/Imgs/Aufover-cile2.png?raw=true)

The OSLC specification used:

![The OSLC specification used](https://github.com/tkratochvila/VerifyAll/blob/main/WebApp/Imgs/Aufover-OSLC.png?raw=true)

# Developer settup guide
The webapp project counts on using full IIS that is part of an windows 10 & 8 operating systems.

This guide will give you some hints on how to setup the IIS to work with this project.

1 - install visual studio with web packages or install them via nuGet packager
2 - the visual studio needs to run under administrator rights
3 - you need to enable the IIS feature in your computer
4 - you need to setup the IIS so that it works with this project
5 - you need to set the proper permissions on directory of this project
6 - you can specify the working directory for temporary files (if you do not want/cannot have the default one) - default one is: "c:\webdir\webapp"
7 - build angular

ad 3)
- type "windows + R"
- type "appwiz.cpl" into text box
- click on "turn windows features on or off"
- check the box: "Internet Information Services"
- expand the box with "Internet Information Services - World Wide Web Services
  - Application Development features" and make sure following boxes are
    checked: ".NET Extensibility 4.8"; "ASP.NET 4.8"; "WebSOcket Protocol"

ad 4)
- search for "inetmgr" and run as administrator
- in "Connection" section expand: "[your computer name] - Application Pools"
- right click on "Application Pools" and select "Add Application Pool" and fill following:
	- "Name" = "WebApp"
	- ".NET CLR version" = ".NET CLR Version v4*"
	- "Managed pipeline mode" = "Integrated"
	- check "Start application pool immediately"

- in "Connection" section expand: "[your computer name] - Sites"
- right click on "Sites" and select "Add websites" and fill in following:
	- "Site name" = "WebApp"
	- "Application pool" = "WebApp"
	- "Physical path" = "[path to a folder with the WebApp project]"
	- "Type" = "https"
	- "IP address" = "All Unassigned"
	- "Port" = "443"
	- "Host name" = ""
	- "SSL certificate" = "IIS Express Development Certificate"
- if it has an error that another site uses this adress and port first do "#1" or "#2" and then try this step ("Add websites") again
- click on "WebApp" site and in "WebApp Home" section double click on "Configuration editor"
	- in "Section" dropdown menu select: "system.webServer/webSocket"
	- make sure the "enabled" property is set to true
- go to "WebApp" site and double click on "Authentication" icon in a "WebApp Home"/"IIS" section in the middle
	- highligh the "Anonymous Authentication" line
		- make sure the "Anonymous Authentication" is enabled - if not, hit "enable" in "Actions" section on the right
		- click on "edit" in "Actions" section on the right and in a dialog select "Application pool identity" option and hit "OK"

ad 5)
- in "File explorer" or "TotalCommander" or other file manager
- right click on "VerifyAll" folder (with whole solution) and click on "properties"
- select tab "Security"
- click on "Edit" button
- click on "Add" button
- click "Locations" and select your computer name
- into TextBox type: "IIS APPPOOL\WebApp" and hit "Check Names"
- click "OK"
- select the created user in list and allow "Full control" for it
- hit "Apply"

ad 6)
- in "WebApp" project go to "web.config" file
- look for <appSettings> tag
- inside this tag is: "<add key="workingDirectory" value="c:\\webdir\\webapp" />"
- change the value property for one of your choosing (! the backslash is escape character and should be doubled)
- if you change it to some directory with limited permissions you have to set the permission for that directory (same as in point 5.) 

ad 7) 
- in PowerShell:
  - change directory to client appication: cd "AUFOVER_GIT\VerifyAll\WebApp\ClientApp"
  - choco install angular
  - npm install
  - ng build
- in Visual Studio right click on WebApp project and "set as startup project" and build project

#1) disabling the conflicting site
- go to the conflicting site in the "Sites" folder within the IIS manager and click on it
- in "Manage Website" section on the right hit "stop" - this site will be unreachable from now on

#2) adressing conflicting site or this new one differently
- it is up to you to manage it properly!


troubleshooting ()
Error message 401.3: You do not have permission to view this directory or page using the credentials you supplied
	- search for "inetmgr" and run as administrator
	- in "WebApp" project go to
	- Go to Authentication --> Anonymous Authentication --> Edit
	- Make sure Anonymous user identity is set to Application pool identity

site give me 401 error:
	- close visual studio
	- delete file: "[your solution directory]"/.vs/VerifyAll/config/applicationhost.config
	- launch visual studio again

"Unable to start debugging on the web server. The underlaying connection was closed. An unexpected error occured on a send." or "This site can be reached"
	- in IIS Manager go to your site and on the right in "Edit site" section click on "Bindings"
	- select the binding and hit "Edit"
	- make sure SSL certificate is set to "IIS Express Development Certificate"


The compiler failed with error code 255 or another number
	- Remove the compilers tag from the web.config file:

	- <compilers>
    		<compiler language="c#;cs;csharp" extension=".cs" type="Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider,  Microsoft.CodeDom.Providers.DotNetCompilerPlatform, Version=1.0.4.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" warningLevel="4" compilerOptions="/langversion:6 /nowarn:1659;1699;1701" />
    		<compiler language="vb;vbs;visualbasic;vbscript" extension=".vb"  type="Microsoft.CodeDom.Providers.DotNetCompilerPlatform.VBCodeProvider,     Microsoft.CodeDom.Providers.DotNetCompilerPlatform, Version=1.0.4.0,     Culture=neutral, PublicKeyToken=31bf3856ad364e35" warningLevel="4"     compilerOptions="/langversion:14 /nowarn:41008     /define:_MYTYPE=\&quot;Web\&quot; /optionInfer+" />
	- </compilers>

If not possible to install ng using choco, you can use:
https://www.jasongaylord.com/blog/setting-up-your-angular-environment

If there are problems with powershell autentification, then you can use:
https://github.com/PowerShell/PowerShell/releases/tag/v7.2.0
Set in VerifyAll solution>WebApp>Properties>BuildEvents>Post build event command line.

Add EARS grammar for formal requirements (could be any ANTRL4 grammar, which correspond to formal requirement)
Add EARS ANTLR4 Visitor that translates the formal grammar to LTL.
