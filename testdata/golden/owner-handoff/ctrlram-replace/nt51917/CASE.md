# NT51917 CtrlRAM Replace Handoff

NT51917 follows the NT51927 postbuild flow by owner confirmation. No additional
owner files are requested. The canonical inventory records three exact
same-workflow aliases without copying payloads:

- FW 1.4.1 single -> NT51927 AUTO_PRJ-529 direct golden;
- FW 1.3.2 two-chip -> NT51927 JIRA-0251 direct input evidence;
- FW 1.4.0 three-chip -> NT51927 PID `0x570A` direct input evidence.

The multi-chip sources are expected-derived evidence and do not become
independent expected goldens. All aliases remain support-neutral.
