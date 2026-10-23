import 'package:flutter/material.dart';

import '../core/theme/app_theme.dart';

/// One tab of [DesignNavBar].
class NavTab {
  const NavTab({
    required this.label,
    required this.icon,
    required this.selectedIcon,
  });

  final String label;
  final IconData icon;
  final IconData selectedIcon;
}

/// The bottom bar of the design: five tabs on a white bar with a hairline on top. The selected tab turns teal and gets a teal line on
/// the top edge (not colour alone: the line and the bold label say it too). The middle tab, Home, is a raised teal circle with a white
/// icon, its label below it.
class DesignNavBar extends StatelessWidget {
  const DesignNavBar({
    required this.tabs,
    required this.selectedIndex,
    required this.onSelected,
    this.raisedIndex = 2,
    super.key,
  });

  final List<NavTab> tabs;
  final int selectedIndex;
  final ValueChanged<int> onSelected;

  /// The tab drawn as the raised circle.
  final int raisedIndex;

  static const _barHeight = 68.0;
  static const _circle = 56.0;
  static const _lift = 22.0;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    // Transparent, so the strip the raised circle sticks into shows the page behind it; the white bar is drawn below, and the system
    // gesture area under it is filled in white too.
    return Material(
      color: Colors.transparent,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          // The raised circle sticks out above the bar, so this reserves that room too.
          SizedBox(
            height: _barHeight + _lift,
            child: Stack(
              clipBehavior: Clip.none,
              children: [
                Positioned(
                  left: 0,
                  right: 0,
                  top: _lift,
                  height: _barHeight,
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      color: c.bgPage,
                      border: Border(top: BorderSide(color: c.borderDefault)),
                    ),
                  ),
                ),
                Positioned(
                  left: 0,
                  right: 0,
                  top: _lift,
                  height: _barHeight,
                  child: Row(
                    children: [
                      for (var i = 0; i < tabs.length; i++)
                        Expanded(
                          child: i == raisedIndex
                              ? _RaisedTab(
                                  tab: tabs[i],
                                  selected: i == selectedIndex,
                                  onTap: () => onSelected(i),
                                )
                              : _FlatTab(
                                  tab: tabs[i],
                                  selected: i == selectedIndex,
                                  onTap: () => onSelected(i),
                                ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          Container(
            height: MediaQuery.paddingOf(context).bottom,
            color: c.bgPage,
          ),
        ],
      ),
    );
  }
}

class _FlatTab extends StatelessWidget {
  const _FlatTab({
    required this.tab,
    required this.selected,
    required this.onTap,
  });

  final NavTab tab;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final color = selected ? c.brandAccent : c.textPrimary;
    return Semantics(
      button: true,
      selected: selected,
      label: tab.label,
      excludeSemantics: true,
      onTap: onTap,
      child: InkResponse(
        onTap: onTap,
        containedInkWell: true,
        highlightShape: BoxShape.rectangle,
        child: Stack(
          alignment: Alignment.topCenter,
          children: [
            if (selected)
              Container(
                width: 56,
                height: 3,
                decoration: BoxDecoration(
                  color: c.brandAccent,
                  borderRadius: const BorderRadius.vertical(
                    bottom: Radius.circular(3),
                  ),
                ),
              ),
            Padding(
              padding: const EdgeInsets.only(top: 10),
              child: Column(
                children: [
                  Icon(selected ? tab.selectedIcon : tab.icon, color: color),
                  const SizedBox(height: 4),
                  Text(
                    tab.label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 12,
                      height: 1.2,
                      color: selected ? c.brandAccent : c.textSecondary,
                      fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RaisedTab extends StatelessWidget {
  const _RaisedTab({
    required this.tab,
    required this.selected,
    required this.onTap,
  });

  final NavTab tab;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Semantics(
      button: true,
      selected: selected,
      label: tab.label,
      excludeSemantics: true,
      onTap: onTap,
      child: InkResponse(
        onTap: onTap,
        containedInkWell: false,
        radius: 36,
        child: Stack(
          clipBehavior: Clip.none,
          alignment: Alignment.topCenter,
          children: [
            Positioned(
              top: -DesignNavBar._lift,
              child: Container(
                width: DesignNavBar._circle,
                height: DesignNavBar._circle,
                decoration: BoxDecoration(
                  color: c.brandAccent,
                  shape: BoxShape.circle,
                  // A white ring separates the circle from the bar behind it, and doubles as the selected mark.
                  border: Border.all(color: c.bgPage, width: 4),
                  boxShadow: const [
                    BoxShadow(
                      color: Color(0x33000000),
                      blurRadius: 6,
                      offset: Offset(0, 2),
                    ),
                  ],
                ),
                child: Icon(
                  selected ? tab.selectedIcon : tab.icon,
                  color: c.brandOnAccent,
                  size: 28,
                ),
              ),
            ),
            Positioned(
              bottom: 8,
              child: Text(
                tab.label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 12,
                  height: 1.2,
                  color: selected ? c.brandAccent : c.textSecondary,
                  fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
