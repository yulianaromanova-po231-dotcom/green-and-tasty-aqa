@level3 @group-pre-order
Feature: Removing a participant from the group pre-order

  Level 3. The owner can take a participant out of the group pre-order; the list
  and the grand total are recalculated at once. Only a participant of this very
  group can be removed, only if they exist, and only by the owner.

  Background:
    Given a restaurant serves these dishes:
      | dish    | price |
      | Caesar  | 180   |
      | Tom Yum | 220   |
    And the owner has booked a table for 5 guests

  Rule: Removing a participant updates the list and the total

    @api @smoke
    Scenario: The owner removes one of several guests
      Given these guests have joined the group pre-order:
        | guest  | dishes      | cart confirmed |
        | Andrii | 2 x Caesar  | yes            |
        | Olena  | 1 x Tom Yum | no             |
        | Taras  | 1 x Caesar  | yes            |
      When the owner removes "Olena" from the group pre-order
      Then the owner sees these participants:
        | participant | dishes     | total | cart confirmed |
        | Andrii      | 2 x Caesar | 360   | yes            |
        | Taras       | 1 x Caesar | 180   | yes            |
      And the grand total is 540

    @api
    Scenario: The owner removes a guest who has already confirmed the cart
      Given the owner has pre-ordered "1 x Tom Yum"
      And these guests have joined the group pre-order:
        | guest  | dishes      | cart confirmed |
        | Andrii | 2 x Caesar  | yes            |
        | Taras  | 2 x Tom Yum | yes            |
      When the owner removes "Taras" from the group pre-order
      And the owner submits the group pre-order
      Then the restaurant receives this consolidated pre-order:
        | dish   | quantity |
        | Caesar | 2        |

    @api
    Scenario: The owner removes the last participant
      Given these guests have joined the group pre-order:
        | guest  | dishes     | cart confirmed |
        | Andrii | 2 x Caesar | yes            |
      When the owner removes "Andrii" from the group pre-order
      Then the owner sees that nobody has joined yet

  Rule: Only an existing participant of this group can be removed, and only by the owner

    @api @negative
    Scenario: A guest of another group cannot be removed from this one
      Given another owner has booked the same table later the same day
      And "Maks" has joined the other owner's group pre-order
      When the owner removes "Maks" who belongs to the other group
      Then the owner is told the participant is not part of this group pre-order

    @api @negative
    Scenario: A participant who never joined cannot be removed
      When the owner removes a participant who never joined
      Then the owner is told that the participant could not be found

    @api @negative
    Scenario Outline: Somebody other than the owner tries to remove a guest
      Given these guests have joined the group pre-order:
        | guest  | dishes     | cart confirmed |
        | Andrii | 2 x Caesar | yes            |
      When <requester> tries to remove "Andrii"
      Then the request is <outcome>

      Examples:
        | requester        | outcome                      |
        | another customer | refused as not the owner     |
        | the waiter       | refused as not the owner     |
        | a visitor        | turned away as not signed in |

    # Not defined by the homework; chosen behaviour (see README): the owner's own place cannot be removed.
    @api @negative
    Scenario: The owner cannot remove their own place in the group
      When the owner tries to remove their own place in the group
      Then the owner is told "The owner's own session cannot be removed from the group."

    # Not defined by the homework; chosen behaviour (see README): a submitted group is frozen.
    @api @negative
    Scenario: Nobody can be removed once the pre-order has been submitted
      Given the owner has pre-ordered "1 x Tom Yum"
      And these guests have joined the group pre-order:
        | guest  | dishes     | cart confirmed |
        | Andrii | 2 x Caesar | yes            |
      And the owner has submitted the group pre-order
      When the owner removes "Andrii" from the group pre-order
      Then the owner is told "Group reservation was submitted."

  Rule: The owner removes a guest on the group pre-order screen

    @ui
    Scenario: The owner removes a guest on screen and the totals follow
      Given these guests have joined the group pre-order:
        | guest  | dishes      | cart confirmed |
        | Andrii | 2 x Caesar  | yes            |
        | Olena  | 1 x Tom Yum | yes            |
      And the owner reviews the group pre-order
      When the owner removes "Olena" from the group pre-order
      Then the owner sees these participants:
        | participant | dishes     | total | cart confirmed |
        | Andrii      | 2 x Caesar | 360   | yes            |
      And the screen shows a grand total of 360

    @ui @e2e
    Scenario: From the guests' carts to the waiter, without the removed guest
      Given the owner has pre-ordered "1 x Tom Yum"
      And these guests have joined the group pre-order:
        | guest  | dishes      | cart confirmed |
        | Andrii | 2 x Caesar  | yes            |
        | Olena  | 2 x Tom Yum | yes            |
        | Taras  | 1 x Caesar  | yes            |
      And the owner reviews the group pre-order
      When the owner removes "Olena" from the group pre-order
      And the owner submits the group pre-order
      Then the screen shows the pre-order as sent to the restaurant
      And the waiter sees this pre-order for the booking:
        | dish    | quantity |
        | Caesar  | 3        |
        | Tom Yum | 1        |
