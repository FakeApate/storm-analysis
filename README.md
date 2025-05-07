---
cover: >-
  https://images.unsplash.com/photo-1708440982415-68a51eba994a?crop=entropy&cs=srgb&fm=jpg&ixid=M3wxOTcwMjR8MHwxfHNlYXJjaHw0fHxyb3BlJTIwY2hhb3N8ZW58MHx8fHwxNzQ2NjQ4NTQ1fDA&ixlib=rb-4.1.0&q=85
coverY: -312
---

# Intro

This document provides an analysis of a newly\* discovered malware identified in the wild. It uses interesting techniques for payload obfuscation and ensures reliable communication with its command and control (C2) server. Initially, the malware was spread via a [certified email](https://en.wikipedia.org/wiki/Certified_email) containing a [JScript](https://en.wikipedia.org/wiki/JScript) attachment. Subsequent stages of the malware's operation were decoded, with each stage building on the previous one. The successful execution of the JScript allowed for data exchange between the threat actor and the victim. Minutes later, the malware attempted to connect to a flagged malicious IP address, leading to isolation.

\*new to me :upside\_down:
