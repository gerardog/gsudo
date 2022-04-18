"use strict";(self.webpackChunkdocs=self.webpackChunkdocs||[]).push([[819],{3905:function(e,t,n){n.d(t,{Zo:function(){return c},kt:function(){return m}});var o=n(7294);function r(e,t,n){return t in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function a(e,t){var n=Object.keys(e);if(Object.getOwnPropertySymbols){var o=Object.getOwnPropertySymbols(e);t&&(o=o.filter((function(t){return Object.getOwnPropertyDescriptor(e,t).enumerable}))),n.push.apply(n,o)}return n}function s(e){for(var t=1;t<arguments.length;t++){var n=null!=arguments[t]?arguments[t]:{};t%2?a(Object(n),!0).forEach((function(t){r(e,t,n[t])})):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(n)):a(Object(n)).forEach((function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(n,t))}))}return e}function i(e,t){if(null==e)return{};var n,o,r=function(e,t){if(null==e)return{};var n,o,r={},a=Object.keys(e);for(o=0;o<a.length;o++)n=a[o],t.indexOf(n)>=0||(r[n]=e[n]);return r}(e,t);if(Object.getOwnPropertySymbols){var a=Object.getOwnPropertySymbols(e);for(o=0;o<a.length;o++)n=a[o],t.indexOf(n)>=0||Object.prototype.propertyIsEnumerable.call(e,n)&&(r[n]=e[n])}return r}var l=o.createContext({}),u=function(e){var t=o.useContext(l),n=t;return e&&(n="function"==typeof e?e(t):s(s({},t),e)),n},c=function(e){var t=u(e.components);return o.createElement(l.Provider,{value:t},e.children)},d={inlineCode:"code",wrapper:function(e){var t=e.children;return o.createElement(o.Fragment,{},t)}},p=o.forwardRef((function(e,t){var n=e.components,r=e.mdxType,a=e.originalType,l=e.parentName,c=i(e,["components","mdxType","originalType","parentName"]),p=u(n),m=r,f=p["".concat(l,".").concat(m)]||p[m]||d[m]||a;return n?o.createElement(f,s(s({ref:t},c),{},{components:n})):o.createElement(f,s({ref:t},c))}));function m(e,t){var n=arguments,r=t&&t.mdxType;if("string"==typeof e||r){var a=n.length,s=new Array(a);s[0]=p;var i={};for(var l in t)hasOwnProperty.call(t,l)&&(i[l]=t[l]);i.originalType=e,i.mdxType="string"==typeof e?e:r,s[1]=i;for(var u=2;u<a;u++)s[u]=n[u];return o.createElement.apply(null,s)}return o.createElement.apply(null,n)}p.displayName="MDXCreateElement"},7031:function(e,t,n){n.r(t),n.d(t,{assets:function(){return c},contentTitle:function(){return l},default:function(){return m},frontMatter:function(){return i},metadata:function(){return u},toc:function(){return d}});var o=n(7462),r=n(3366),a=(n(7294),n(3905)),s=["components"],i={sidebar_position:0,title:"Usage from WSL"},l=void 0,u={unversionedId:"usage/wsl",id:"usage/wsl",title:"Usage from WSL",description:"On WSL (Windows Subsystem for Linux), elevation and root are different concepts. root allows full administration of WSL but not the windows system. Use WSL's native su or sudo to gain root access. But to get admin privilege on the Windows box you need to elevate the WSL.EXE process. gsudo allows that (a UAC popup will appear).",source:"@site/docs/usage/wsl.md",sourceDirName:"usage",slug:"/usage/wsl",permalink:"/gsudo/docs/usage/wsl",editUrl:"https://github.com/gerardog/gsudo/blob/docs/docs/docs/usage/wsl.md",tags:[],version:"current",sidebarPosition:0,frontMatter:{sidebar_position:0,title:"Usage from WSL"},sidebar:"tutorialSidebar",previous:{title:"Usage from PowerShell",permalink:"/gsudo/docs/usage/powershell"},next:{title:"MinGW / MSYS2 / Git-Bash / Cygwin",permalink:"/gsudo/docs/usage/mingw-msys2"}},c={},d=[],p={toc:d};function m(e){var t=e.components,n=(0,r.Z)(e,s);return(0,a.kt)("wrapper",(0,o.Z)({},p,n,{components:t,mdxType:"MDXLayout"}),(0,a.kt)("p",null,"On WSL (Windows Subsystem for Linux), elevation and ",(0,a.kt)("inlineCode",{parentName:"p"},"root")," are different concepts. ",(0,a.kt)("inlineCode",{parentName:"p"},"root")," allows full administration of WSL but not the windows system. Use WSL's native ",(0,a.kt)("inlineCode",{parentName:"p"},"su")," or ",(0,a.kt)("inlineCode",{parentName:"p"},"sudo")," to gain ",(0,a.kt)("inlineCode",{parentName:"p"},"root")," access. But to get admin privilege on the Windows box you need to elevate the WSL.EXE process. ",(0,a.kt)("inlineCode",{parentName:"p"},"gsudo")," allows that (a UAC popup will appear)."),(0,a.kt)("p",null,"On WSL bash, prepend ",(0,a.kt)("inlineCode",{parentName:"p"},"gsudo")," to elevate ",(0,a.kt)("strong",{parentName:"p"},"WSL commands")," or ",(0,a.kt)("inlineCode",{parentName:"p"},"gsudo -d")," for ",(0,a.kt)("strong",{parentName:"p"},"CMD commands"),". "),(0,a.kt)("pre",null,(0,a.kt)("code",{parentName:"pre",className:"language-bash"},'# elevate default shell\nPC:~$ gsudo \n\n# run elevated WSL command\nPC:~$ gsudo mkdir /mnt/c/Windows/MyFolder\n\n# run elevated Windows command\nPC:~$ gsudo -d notepad C:/Windows/System32/drivers/etc/hosts\nPC:~$ gsudo -d "notepad C:\\Windows\\System32\\drivers\\etc\\hosts"\nPC:~$ gsudo -d "echo 127.0.0.1 www.MyWeb.com >> %windir%\\System32\\drivers\\etc\\hosts"\n\n# test for gsudo and command success\nretval=$?;\nif [ $retval -eq 0 ]; then\n    echo "Success";\nelif [ $retval -eq $((999 % 256)) ]; then # gsudo failure exit code (999) is read as 231 on wsl (999 mod 256)\n    echo "gsudo failed to elevate!";\nelse\n    echo "Command failed with exit code $retval";\nfi;\n')))}m.isMDXComponent=!0}}]);