import React from 'react';
import clsx from 'clsx';
import styles from './styles.module.css';

const FeatureList = [
  {
    title: 'Easy to Use', link: "docs/usage",
    description: (
      <>
        Prepend <code>gsudo</code> to make your command run elevated <b>in the current console window</b>. Just as Unix/Linux sudo. One UAC popup will appear. 
        <br/><a href='docs/usage'>Learn more</a>
      </>
    ),
  },  {
    title: 'Easy to Install', link: "docs/install",
    description: (
      <>
        Using Chocolatey: <code>choco install gsudo</code><br/>
        Using Scoop: <code>scoop install gsudo</code><br/>
        Using WinGet: <code>winget install gerardog.gsudo</code><br/>
        <a href='docs/install'>Learn how to install.</a>
      </>
    ),
  },  {
    title: 'Portable',
    description: (
      <>
        gsudo is just a portable console app. No Windows service is required or system change is done, except adding gsudo to the Path.
      </>
    ),
  },  {
    title: 'Supports your preferred shell',
    description: (
      <>
        Detects your shell and elevates your command as a native shell command.
        Currently supports <code>CMD</code>, <code>PowerShell</code>, <code>WSL</code>, <code>MinGW/Git-Bash</code>, <code>MSYS2</code>, <code>Cygwin</code>,<code>Yori</code> and <code>Take Command</code>.
      </>
    ),
  },  {
    title: 'Credentials Cache', link: "docs/credentials-cache",
    description: (
      <>
        Too many UAC pop-ups? You can see less popups if you opt-in to enable the <code>credentials cache</code>, once you understand the security implications.
        <br/><a href='docs/credentials-cache'>Learn more.</a>
      </>
    ),
  },  {
    title: 'Increase your productivity',
    description: (
      <>
        Do not waste time opening a new window and switching context back and forth. Also avoid the "elevation fatigue" that leads to the malpractice of running <b>everything</b> elevated, and just elevate specific commands.
      </>
    ),
  },
];

function Feature({Svg, title, description, link}) {
  return (
    <div className={clsx('col col--4')}>
{/* <!--
      <div className="text--center">
        <Svg className={styles.featureSvg} role="img" />
      </div>
--> */}
      <div className="text--center padding-horiz--md">
        <h3>{title}</h3>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
