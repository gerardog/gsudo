"use strict";(self.webpackChunkdocs=self.webpackChunkdocs||[]).push([[183],{3905:function(e,t,n){n.d(t,{Zo:function(){return u},kt:function(){return f}});var r=n(7294);function o(e,t,n){return t in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function a(e,t){var n=Object.keys(e);if(Object.getOwnPropertySymbols){var r=Object.getOwnPropertySymbols(e);t&&(r=r.filter((function(t){return Object.getOwnPropertyDescriptor(e,t).enumerable}))),n.push.apply(n,r)}return n}function i(e){for(var t=1;t<arguments.length;t++){var n=null!=arguments[t]?arguments[t]:{};t%2?a(Object(n),!0).forEach((function(t){o(e,t,n[t])})):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(n)):a(Object(n)).forEach((function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(n,t))}))}return e}function s(e,t){if(null==e)return{};var n,r,o=function(e,t){if(null==e)return{};var n,r,o={},a=Object.keys(e);for(r=0;r<a.length;r++)n=a[r],t.indexOf(n)>=0||(o[n]=e[n]);return o}(e,t);if(Object.getOwnPropertySymbols){var a=Object.getOwnPropertySymbols(e);for(r=0;r<a.length;r++)n=a[r],t.indexOf(n)>=0||Object.prototype.propertyIsEnumerable.call(e,n)&&(o[n]=e[n])}return o}var c=r.createContext({}),l=function(e){var t=r.useContext(c),n=t;return e&&(n="function"==typeof e?e(t):i(i({},t),e)),n},u=function(e){var t=l(e.components);return r.createElement(c.Provider,{value:t},e.children)},p={inlineCode:"code",wrapper:function(e){var t=e.children;return r.createElement(r.Fragment,{},t)}},d=r.forwardRef((function(e,t){var n=e.components,o=e.mdxType,a=e.originalType,c=e.parentName,u=s(e,["components","mdxType","originalType","parentName"]),d=l(n),f=o,m=d["".concat(c,".").concat(f)]||d[f]||p[f]||a;return n?r.createElement(m,i(i({ref:t},u),{},{components:n})):r.createElement(m,i({ref:t},u))}));function f(e,t){var n=arguments,o=t&&t.mdxType;if("string"==typeof e||o){var a=n.length,i=new Array(a);i[0]=d;var s={};for(var c in t)hasOwnProperty.call(t,c)&&(s[c]=t[c]);s.originalType=e,s.mdxType="string"==typeof e?e:o,i[1]=s;for(var l=2;l<a;l++)i[l]=n[l];return r.createElement.apply(null,i)}return r.createElement.apply(null,n)}d.displayName="MDXCreateElement"},7973:function(e,t,n){n.r(t),n.d(t,{assets:function(){return u},contentTitle:function(){return c},default:function(){return f},frontMatter:function(){return s},metadata:function(){return l},toc:function(){return p}});var r=n(7462),o=n(3366),a=(n(7294),n(3905)),i=["components"],s={sidebar_position:4,id:"credentials-cache",title:"Credentials Cache"},c=void 0,l={unversionedId:"credentials-cache",id:"credentials-cache",title:"Credentials Cache",description:"The Credentials Cache allows to elevate several times from a parent process with only one UAC pop-up.",source:"@site/docs/credentials-cache.md",sourceDirName:".",slug:"/credentials-cache",permalink:"/gsudo/docs/credentials-cache",editUrl:"https://github.com/gerardog/gsudo/blob/docs/docs/docs/credentials-cache.md",tags:[],version:"current",sidebarPosition:4,frontMatter:{sidebar_position:4,id:"credentials-cache",title:"Credentials Cache"},sidebar:"tutorialSidebar",previous:{title:"MinGW / MSYS2 / Git-Bash / Cygwin",permalink:"/gsudo/docs/usage/mingw-msys2"},next:{title:"Security Considerations",permalink:"/gsudo/docs/security"}},u={},p=[],d={toc:p};function f(e){var t=e.components,n=(0,o.Z)(e,i);return(0,a.kt)("wrapper",(0,r.Z)({},d,n,{components:t,mdxType:"MDXLayout"}),(0,a.kt)("p",null,"The ",(0,a.kt)("inlineCode",{parentName:"p"},"Credentials Cache")," allows to elevate several times from a parent process with only one UAC pop-up.  "),(0,a.kt)("p",null,"An active credentials cache session is just an elevated instance of gsudo that stays running and allows the invoker process to elevate again. No windows service or setup involved."),(0,a.kt)("p",null,"It is convenient, but it's safe only if you are not already hosting a malicious process: No matter how secure gsudo itself is, a malicious process could ",(0,a.kt)("a",{parentName:"p",href:"https://en.wikipedia.org/wiki/DLL_injection#Approaches_on_Microsoft_Windows"},"trick")," the allowed process (Cmd/Powershell) and force a running ",(0,a.kt)("inlineCode",{parentName:"p"},"gsudo")," cache instance to elevate silently."),(0,a.kt)("p",null,(0,a.kt)("strong",{parentName:"p"},"Cache Modes:")),(0,a.kt)("ul",null,(0,a.kt)("li",{parentName:"ul"},(0,a.kt)("strong",{parentName:"li"},"Explicit: (default)")," Every elevation shows a UAC popup, unless a cache session is started explicitly with ",(0,a.kt)("inlineCode",{parentName:"li"},"gsudo cache on"),"."),(0,a.kt)("li",{parentName:"ul"},(0,a.kt)("strong",{parentName:"li"},"Auto:")," Simil-unix-sudo. The first elevation shows a UAC Popup and starts a cache session automatically."),(0,a.kt)("li",{parentName:"ul"},(0,a.kt)("strong",{parentName:"li"},"Disabled:")," Every elevation request shows a UAC popup.")),(0,a.kt)("p",null,"The cache mode can be set with ",(0,a.kt)("strong",{parentName:"p"},(0,a.kt)("inlineCode",{parentName:"strong"},"gsudo config CacheMode auto|explicit|disabled"))),(0,a.kt)("p",null,"Use ",(0,a.kt)("inlineCode",{parentName:"p"},"gsudo cache on|off")," to start/stop a cache session manually (i.e. allow/disallow elevation of the current process with no additional UAC popups)."),(0,a.kt)("p",null,"Use ",(0,a.kt)("inlineCode",{parentName:"p"},"gsudo -k")," to terminate all cache sessions. (Use this before leaving your computer unattended to someone else.)"),(0,a.kt)("p",null,"The cache session ends automatically when the allowed process ends or if no elevations requests are received for 5 minutes (configurable via ",(0,a.kt)("inlineCode",{parentName:"p"},"gsudo config CacheDuration"),")."))}f.isMDXComponent=!0}}]);