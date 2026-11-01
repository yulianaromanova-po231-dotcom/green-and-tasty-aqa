@level2 @group-pre-order
Feature: Submitting the consolidated group pre-order

  Level 2. Once the guests have chosen, the owner sends one consolidated pre-order
  to the restaurant. Only confirmed carts count; the same dish ordered by several
  people becomes one line with the summed quantity and is added to the booking's
  pre-order, which the waiter then sees.

  Background:
    Given a restaurant serves these dishes:
      | dish    | price |
      | Caesar  | 180   |
      | Tom Yum | 220   |
    And the owner has booked a table for 5 guests

  Rule: Confirmed carts are consolidated into the booking's pre-order

    Background:
      Given the owner has pre-ordered "1 x Tom Yum"
      And these guests have joined the group pre-order:
        | guest  | dishes                  | cart confirmed |
        | Andrii | 2 x Caesar              | yes            |
        | Taras  | 1 x Caesar, 2 x Tom Yum | yes            |
        | Olena  | 3 x Tom Yum             | no             |

    @api @smoke
    Scenario: Same dishes from different guests are merged and unconfirmed carts are left out
      When the owner submits the group pre-order
      Then the restaurant receives this consolidated pre-order:
        | dish    | quantity |
        | Caesar  | 3        |
        | Tom Yum | 2        |
      And the booking's pre-order contains:
        | dish    | quantity |
        | Caesar  | 3        |
        | Tom Yum | 3        |
      And the booking's pre-order is marked as submitted by the customer
      And the group pre-order is marked as submitted

    @api
    Scenario: The waiter sees the pre-order the owner submitted
      Given the owner has submitted the group pre-order
      Then the waiter sees this pre-order for the booking:
        | dish    | quantity |
        | Caesar  | 3        |
        | Tom Yum | 3        |

    @api @negative
    Scenario: The owner cannot submit the same group pre-order twice
      Given the owner has submitted the group pre-order
      When the owner submits the group pre-order
      Then the owner is told "Group reservation was submitted."
      And the booking's pre-order contains:
        | dish    | quantity |
        | Caesar  | 3        |
        | Tom Yum | 3        |

    @api @negative
    Scenario Outline: Somebody other than the owner tries to submit
      When <requester> tries to submit the group pre-order
      Then the request is <outcome>
      And the group pre-order is still open

      Examples:
        | requester        | outcome                      |
        | another customer | refused as not the owner     |
        | a visitor        | turned away as not signed in |

    @ui @smoke
    Scenario: The owner submits on screen and the screen reports it as sent
      Given the owner reviews the group pre-order
      When the owner submits the group pre-order
      Then the screen shows the pre-order as sent to the restaurant
      And the screen consolidates only the confirmed carts:
        | dish    | quantity |
        | Caesar  | 3        |
        | Tom Yum | 2        |
      And the booking's pre-order is marked as submitted by the customer

    @ui
    Scenario: The waiter sees the submitted pre-order in the waiter's screens
      Given the owner has submitted the group pre-order
      Then the waiter sees this pre-order for the booking:
        | dish    | quantity |
        | Caesar  | 3        |
        | Tom Yum | 3        |

  Rule: A group pre-order that cannot be fulfilled is not submitted

    @api @negative
    Scenario: Nobody has confirmed a cart
      Given the owner has pre-ordered "1 x Tom Yum"
      And these guests have joined the group pre-order:
        | guest | dishes     | cart confirmed |
        | Olena | 2 x Caesar | no             |
      When the owner submits the group pre-order
      Then the owner is told "There are no submitted guest sessions to consolidate."
      And the group pre-order is still open

    # The backend refuses to confirm an empty cart, so "confirmed carts without any dish" cannot
    # be reached through the application: this scenario checks the guard that makes it impossible.
    @api @negative
    Scenario: A guest cannot confirm a cart without dishes
      Given the owner has pre-ordered "1 x Tom Yum"
      And "Mariia" has joined the group pre-order
      When "Mariia" tries to confirm the cart
      Then "Mariia" is told "The cart is empty"

    @api @negative
    Scenario: The owner has not pre-ordered for the booking
      Given these guests have joined the group pre-order:
        | guest  | dishes     | cart confirmed |
        | Andrii | 2 x Caesar | yes            |
      When the owner submits the group pre-order
      Then the owner is told that the booking's pre-order could not be found
      And the group pre-order is still open

    @api @negative
    Scenario: The booking does not exist
      Given the owner has pre-ordered "1 x Tom Yum"
      When the owner submits the group pre-order for a booking that does not exist
      Then the owner is told that the booking could not be found

    @api @negative
    Scenario: The group pre-order does not exist
      Given the owner has pre-ordered "1 x Tom Yum"
      When the owner submits a group pre-order that does not exist
      Then the owner is told that the group pre-order could not be found

    @ui @negative
    Scenario: The screen does not let the owner submit before anyone has confirmed a cart
      Given the owner has pre-ordered "1 x Tom Yum"
      And these guests have joined the group pre-order:
        | guest | dishes     | cart confirmed |
        | Olena | 2 x Caesar | no             |
      When the owner reviews the group pre-order
      Then the owner cannot submit the group pre-order
