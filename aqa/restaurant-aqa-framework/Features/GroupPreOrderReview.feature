@level1 @group-pre-order
Feature: Reviewing the group pre-order

  Level 1. The owner of a booking for several people invites guests; every guest
  joins by the invitation and fills in a cart of their own. The owner sees who has
  joined, what each of them chose, how much each cart comes to and the total for
  the whole group — and nobody else may see it.

  Background:
    Given a restaurant serves these dishes:
      | dish    | price |
      | Caesar  | 180   |
      | Tom Yum | 220   |
    And the owner has booked a table for 5 guests

  Rule: The owner sees every participant, their dishes and totals

    @api @smoke
    Scenario: The owner sees each guest's dishes, quantities and total
      Given these guests have joined the group pre-order:
        | guest  | dishes                | cart confirmed |
        | Andrii | 2 x Caesar            | yes            |
        | Olena  | 1 x Tom Yum           | no             |
        | Taras  | 1 x Caesar, 2 x Tom Yum | yes          |
      When the owner reviews the group pre-order
      Then the owner sees these participants:
        | participant | dishes                  | total | cart confirmed |
        | Andrii      | 2 x Caesar              | 360   | yes            |
        | Olena       | 1 x Tom Yum             | 220   | no             |
        | Taras       | 1 x Caesar, 2 x Tom Yum | 620   | yes            |
      And the grand total is 1200
      And the grand total equals the sum of the participants' totals

    @api
    Scenario: The grand total follows a guest changing their cart
      Given these guests have joined the group pre-order:
        | guest  | dishes      | cart confirmed |
        | Andrii | 2 x Caesar  | yes            |
        | Olena  | 1 x Tom Yum | no             |
      When "Olena" adds 1 "Caesar" to the cart
      And the owner reviews the group pre-order
      Then the owner sees these participants:
        | participant | dishes                  | total | cart confirmed |
        | Andrii      | 2 x Caesar              | 360   | yes            |
        | Olena       | 1 x Caesar, 1 x Tom Yum | 400   | no             |
      And the grand total is 760

    @api
    Scenario: A guest who has not confirmed the cart or has no dishes is still listed
      Given these guests have joined the group pre-order:
        | guest  | dishes      | cart confirmed |
        | Andrii | 2 x Caesar  | yes            |
        | Olena  | 1 x Tom Yum | no             |
        | Mariia |             | no             |
      When the owner reviews the group pre-order
      Then the owner sees these participants:
        | participant | dishes      | total | cart confirmed |
        | Andrii      | 2 x Caesar  | 360   | yes            |
        | Olena       | 1 x Tom Yum | 220   | no             |
        | Mariia      |             | 0     | no             |

    @api
    Scenario: Nobody has joined the group yet
      When the owner reviews the group pre-order
      Then the owner sees that nobody has joined yet

  Rule: Only the owner of the booking may see the group pre-order

    @api @negative
    Scenario Outline: Somebody other than the owner asks to see the group pre-order
      Given these guests have joined the group pre-order:
        | guest  | dishes     | cart confirmed |
        | Andrii | 2 x Caesar | yes            |
      When <requester> asks to see the group pre-order
      Then the request is <outcome>

      Examples: Signed in, but not the owner
        | requester        | outcome                  |
        | another customer | refused as not the owner |
        | the waiter       | refused as not the owner |

      Examples: Not signed in as a customer
        | requester | outcome                     |
        | a guest   | turned away as not signed in |
        | a visitor | turned away as not signed in |

    @api @negative
    Scenario: The owner asks for a group pre-order that does not exist
      When the owner reviews a group pre-order that does not exist
      Then the owner is told that the group pre-order could not be found

  Rule: The owner reviews the group pre-order on the group pre-order screen

    @ui @smoke
    Scenario: The owner sees the guests, their carts and the consolidated total on screen
      Given these guests have joined the group pre-order:
        | guest  | dishes                  | cart confirmed |
        | Andrii | 2 x Caesar              | yes            |
        | Olena  | 1 x Tom Yum             | no             |
        | Taras  | 1 x Caesar, 2 x Tom Yum | yes            |
      When the owner reviews the group pre-order
      Then the owner sees these participants:
        | participant | dishes                  | total | cart confirmed |
        | Andrii      | 2 x Caesar              | 360   | yes            |
        | Olena       | 1 x Tom Yum             | 220   | no             |
        | Taras       | 1 x Caesar, 2 x Tom Yum | 620   | yes            |
      And the screen consolidates only the confirmed carts:
        | dish    | quantity |
        | Caesar  | 3        |
        | Tom Yum | 2        |
      And the screen shows a grand total of 980

    @ui
    Scenario: The screen picks up a guest's new dish when the owner refreshes it
      Given these guests have joined the group pre-order:
        | guest | dishes      | cart confirmed |
        | Olena | 1 x Tom Yum | no             |
      And the owner reviews the group pre-order
      When "Olena" adds 1 "Caesar" to the cart
      And the owner refreshes the group pre-order screen
      Then the owner sees these participants:
        | participant | dishes                  | total | cart confirmed |
        | Olena       | 1 x Caesar, 1 x Tom Yum | 400   | no             |

    @ui
    Scenario: The screen tells the owner when nobody has added a dish yet
      Given these guests have joined the group pre-order:
        | guest  | dishes | cart confirmed |
        | Mariia |        | no             |
      When the owner reviews the group pre-order
      Then the screen says that nobody has added a dish yet
      And the owner cannot submit the group pre-order

    @ui @negative
    Scenario: Another customer cannot open the owner's group pre-order screen
      When another customer opens the owner's group pre-order screen
      Then the screen says the group pre-order could not be loaded
